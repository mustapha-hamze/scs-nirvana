using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cms.ContentDelivery.PackageTests;

// The packed .nupkg files - not the projects - carry only the delivery contract and the SQL
// adapter, and a consumer restoring them gets no Core layer (Application, Domain, Infrastructure,
// Web, deprecated Services), EF entity or other private assembly, directly or transitively.
public sealed class PackageBoundaryTests
{
    private const string Sdk = "Cms.ContentDelivery", Adapter = "Cms.ContentDelivery.SqlServer";

    private static readonly string Version = Metadata("ContentDeliveryVersion");

    // Everything a package may reach: itself, the SDK, and Microsoft/System framework, EF Core,
    // SQL client and Azure identity packages (Microsoft.Data.SqlClient's own dependencies).
    private static readonly string[] AllowedPackagePrefixes = ["Cms.ContentDelivery", "Microsoft.", "System.", "Azure.", "runtime."];

    // The Core solution's assembly names.
    private static readonly string[] CoreAssemblies = ["Application", "Domain", "Infrastructure", "Web", "Services", "Core.Tests", "Web.Tests"];

    private static string Metadata(string key) =>
        typeof(PackageBoundaryTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == key).Value!;

    private static ZipArchive OpenPackage(string id) =>
        ZipFile.OpenRead(Path.Combine(Metadata("PackageFeed"), $"{id}.{Version}.nupkg"));

    private static XElement Nuspec(ZipArchive package)
    {
        using var stream = package.Entries.Single(e => e.FullName.EndsWith(".nuspec")).Open();
        return XDocument.Load(stream).Root!.Elements().Single(e => e.Name.LocalName == "metadata");
    }

    private static string? Value(XElement metadata, string name) => metadata.Elements().SingleOrDefault(e => e.Name.LocalName == name)?.Value;

    private static byte[] Library(ZipArchive package, string id)
    {
        using var stream = package.GetEntry($"lib/net9.0/{id}.dll")!.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    [Theory]
    [InlineData(Sdk)]
    [InlineData(Adapter)]
    public void Package_IsVersionedWithConsumerMetadata(string id)
    {
        using var package = OpenPackage(id);
        var metadata = Nuspec(package);

        Assert.Equal(id, Value(metadata, "id"));
        Assert.Equal(Version, Value(metadata, "version"));
        Assert.Contains("-", Version);
        foreach (var field in new[] { "authors", "description", "releaseNotes", "tags" })
            Assert.False(string.IsNullOrWhiteSpace(Value(metadata, field)), field);
        Assert.Equal("https://github.com/mustapha-hamze/scs-nirvana", metadata.Elements().Single(e => e.Name.LocalName == "repository").Attribute("url")?.Value);
    }

    [Theory]
    [InlineData(Sdk)]
    [InlineData(Adapter)]
    public void Package_ShipsOnlyItsOwnNet9Assembly(string id)
    {
        using var package = OpenPackage(id);

        var payload = package.Entries.Select(e => e.FullName)
            .Where(name => !name.EndsWith(".nuspec") && !name.StartsWith("_rels/") && !name.StartsWith("package/") && name != "[Content_Types].xml");
        Assert.Equal([$"lib/net9.0/{id}.dll"], payload);
    }

    [Fact]
    public void PackageDependencies_AreOnlyTheSdkAndFrameworkProviders()
    {
        using var sdk = OpenPackage(Sdk);
        using var adapter = OpenPackage(Adapter);

        Assert.Equal([("Microsoft.Extensions.Options.ConfigurationExtensions", "9.0.20")], Dependencies(sdk));
        Assert.Equal(
            [(Sdk, $"[{Version}]"), ("Microsoft.EntityFrameworkCore.SqlServer", "9.0.20"), ("Microsoft.Extensions.Diagnostics.HealthChecks", "9.0.9")],
            Dependencies(adapter));

        static IEnumerable<(string, string)> Dependencies(ZipArchive package)
        {
            var groups = Nuspec(package).Descendants().Where(e => e.Name.LocalName == "group").ToList();
            Assert.Equal(["net9.0"], groups.Select(g => g.Attribute("targetFramework")!.Value));
            return groups.Single().Elements().Select(d => (d.Attribute("id")!.Value, d.Attribute("version")!.Value)).Order();
        }
    }

    [Theory]
    [InlineData(Sdk, new[] { "Microsoft.Extensions.Options", "Microsoft.Extensions.Configuration", "Microsoft.Extensions.DependencyInjection.Abstractions" })]
    [InlineData(Adapter, new[] { Sdk, "Microsoft.Extensions.", "Microsoft.EntityFrameworkCore" })]
    public void PackedAssembly_ReferencesNoCoreOrNewerThanNet9Assembly(string id, string[] allowed)
    {
        using var package = OpenPackage(id);
        using var reader = new PEReader(new MemoryStream(Library(package, id)));
        var metadata = reader.GetMetadataReader();

        var references = metadata.AssemblyReferences.Select(h => metadata.GetAssemblyReference(h))
            .Select(r => (Name: metadata.GetString(r.Name), r.Version))
            .ToList();
        var violations = references
            .Where(r => r.Name != "netstandard" && r.Name != "System" && !r.Name.StartsWith("System.")
                        && !allowed.Any(prefix => r.Name == prefix.TrimEnd('.') || r.Name.StartsWith(prefix.TrimEnd('.') + ".")))
            .Select(r => r.Name);
        Assert.Empty(violations);
        Assert.DoesNotContain(references, r => r.Name != "netstandard" && r.Version.Major > 9);
    }

    // The public surface of the packed binaries (the loaded assemblies are the packed ones, by
    // MVID): SDK contracts plus the adapter's registration class - no DbContext, EF entity or row.
    [Fact]
    public void PackedPublicSurface_IsTheContractAndTheRegistrationOnly()
    {
        var sdk = typeof(IContentDeliveryClient).Assembly;
        var adapter = typeof(SqlServerContentDeliveryServiceCollectionExtensions).Assembly;
        foreach (var (id, assembly) in new[] { (Sdk, sdk), (Adapter, adapter) })
        {
            using var package = OpenPackage(id);
            using var reader = new PEReader(new MemoryStream(Library(package, id)));
            var metadata = reader.GetMetadataReader();
            Assert.Equal(metadata.GetGuid(metadata.GetModuleDefinition().Mvid), assembly.ManifestModule.ModuleVersionId);
        }

        Assert.Equal([typeof(SqlServerContentDeliveryServiceCollectionExtensions)], adapter.GetExportedTypes());
        Assert.All(sdk.GetExportedTypes(), t =>
        {
            Assert.Equal("Cms.ContentDelivery", t.Namespace);
            Assert.False(typeof(DbContext).IsAssignableFrom(t), t.FullName);
        });
        Assert.All(adapter.GetTypes().Where(t => typeof(DbContext).IsAssignableFrom(t) || t.Name.EndsWith("Row")), t => Assert.False(t.IsVisible, t.FullName));
    }

    // The consumer's restore graph: the two delivery packages come from the feed as packages (no
    // project references), and everything they pull in is framework/provider packages.
    [Fact]
    public void ConsumerRestoreGraph_HasOnlyDeliveryPackagesAndFrameworkProviders()
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Metadata("ProjectAssetsFile")));
        var root = assets.RootElement;

        var libraries = root.GetProperty("libraries").EnumerateObject()
            .Select(l => (Name: l.Name.Split('/')[0], PackageVersion: l.Name.Split('/')[1], Type: l.Value.GetProperty("type").GetString()))
            .ToList();
        Assert.All(libraries, l => Assert.Equal("package", l.Type));
        Assert.Contains((Sdk, Version, "package"), libraries);
        Assert.Contains((Adapter, Version, "package"), libraries);

        var direct = root.GetProperty("project").GetProperty("frameworks").GetProperty("net9.0").GetProperty("dependencies")
            .EnumerateObject().Select(d => d.Name).Order();
        Assert.Equal([Sdk, Adapter, "Microsoft.EntityFrameworkCore.Sqlite", "Microsoft.NET.Test.Sdk", "xunit", "xunit.runner.visualstudio"], direct);

        // Walk the delivery packages' closure.
        var graph = root.GetProperty("targets").GetProperty("net9.0").EnumerateObject().ToDictionary(
            t => t.Name.Split('/')[0],
            t => t.Value.TryGetProperty("dependencies", out var d) ? d.EnumerateObject().Select(x => x.Name).ToList() : []);
        var closure = new HashSet<string>();
        var pending = new Stack<string>([Sdk, Adapter]);
        while (pending.TryPop(out var name))
            if (closure.Add(name))
                foreach (var dependency in graph[name])
                    pending.Push(dependency);

        Assert.DoesNotContain(closure, n => !AllowedPackagePrefixes.Any(n.StartsWith));
        Assert.DoesNotContain(closure, CoreAssemblies.Contains);
    }

    [Fact]
    public void ConsumerOutput_ContainsNoCoreAssembly()
    {
        var shipped = Directory.GetFiles(AppContext.BaseDirectory, "*.dll").Select(Path.GetFileNameWithoutExtension).ToList();
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToList();

        Assert.Contains(Sdk, shipped);
        Assert.Contains(Adapter, shipped);
        foreach (var name in shipped.Concat(loaded))
            Assert.DoesNotContain(name, CoreAssemblies);
    }
}
