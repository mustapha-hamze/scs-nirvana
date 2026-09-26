using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace Cms.ContentDelivery.SqlServer.Tests;

// Release gate: the SDK as a website receives it. Runs with the solution's tests. Clears the
// package fixture's (ContentDelivery.PackageTests) local feed, build output and cached
// Cms.ContentDelivery* packages, proves the fixture cannot restore without them, then packs the
// current source, restores the fixture from those fresh .nupkg files only and runs its boundary,
// composition and contract tests. Needs nuget.org (or a warm fixture cache) for third-party packages.
public sealed class PackedConsumerGateTests
{
    private static readonly string Core = typeof(PackedConsumerGateTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "CoreDirectory").Value!;

    private static readonly string Fixture = Path.Combine(Core, "ContentDelivery.PackageTests");

    [Fact]
    [Trait("Category", "PackageGate")]
    public void PackedConsumer_FailsWithoutFreshPackages_AndPassesWithThem()
    {
        var feed = Path.Combine(Fixture, "obj", "feed");
        var cache = Path.Combine(Fixture, "obj", "nuget-packages");
        // bin too: packed files carry a fixed timestamp, so a same-size DLL from a newer pack is
        // skipped by MSBuild's unchanged-file copy and the previous binary would be tested.
        foreach (var stale in new[] { feed, Path.Combine(Fixture, "bin"), Path.Combine(cache, "cms.contentdelivery"), Path.Combine(cache, "cms.contentdelivery.sqlserver") })
            if (Directory.Exists(stale))
                Directory.Delete(stale, recursive: true);
        Directory.CreateDirectory(feed);

        var broken = Dotnet("restore", Fixture, "--force");
        Assert.True(broken.ExitCode != 0 && broken.Output.Contains("NU1101"), $"Restore must fail without freshly packed packages:\n{broken.Output}");

        // --no-restore: the solution restore already covers these projects, and a concurrent
        // restore would rewrite their assets files under a parallel solution build.
        foreach (var project in new[] { "ContentDelivery", "ContentDelivery.SqlServer" })
            Succeeds(Dotnet("pack", Path.Combine(Core, project, $"{project}.csproj"), "-c", "Release", "--no-restore", "-o", feed));

        Succeeds(Dotnet("restore", Fixture, "--force"));
        Succeeds(Dotnet("test", Fixture, "-c", "Release", "--no-restore", "--filter", "Category!=Baseline"));
    }

    private static void Succeeds((int ExitCode, string Output) result) => Assert.True(result.ExitCode == 0, result.Output);

    private static (int ExitCode, string Output) Dotnet(params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Fixture
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        // The test host runs inside an MSBuild/VSTest invocation; don't leak its settings into
        // the nested one.
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || k.StartsWith("VSTEST", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(name);

        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(TimeSpan.FromMinutes(10)))
        {
            process.Kill(entireProcessTree: true);
            return (-1, $"dotnet {string.Join(' ', arguments)} timed out.\n{output}");
        }
        return (process.ExitCode, $"dotnet {string.Join(' ', arguments)}\n{output}{error.Result}");
    }
}
