using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Core.Tests.Architecture;

// Application id 0 must never stand in for "no selected tenant" (see
// CurrentApplicationContextExtensions.RequireApplicationId). This is a source-level guard rather
// than a reflection one, since the banned pattern is a literal C#/Razor expression
// ("CurrentApplicationId ?? 0"), not a runtime-observable type shape.
public class NoZeroTenantFallbackTests
{
    [Fact]
    public void WebSource_DoesNotReintroduceTheZeroApplicationIdFallback()
    {
        var webSourcePath = GetWebSourcePath();

        var offendingFiles = Directory.EnumerateFiles(webSourcePath, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".cshtml")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains("CurrentApplicationId ?? 0"))
            .Select(f => Path.GetRelativePath(webSourcePath, f))
            .ToList();

        Assert.True(offendingFiles.Count == 0,
            $"Found the banned 'CurrentApplicationId ?? 0' fallback - use RequireApplicationId() instead: {string.Join(", ", offendingFiles)}");
    }

    private static string GetWebSourcePath([CallerFilePath] string thisFilePath = "")
    {
        // thisFilePath: .../src/Core/Tests/Architecture/NoZeroTenantFallbackTests.cs
        var srcDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "..", ".."));
        return Path.Combine(srcDir, "Web");
    }
}
