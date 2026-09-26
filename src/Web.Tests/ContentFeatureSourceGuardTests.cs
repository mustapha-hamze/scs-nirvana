using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Web.Tests;

// Web Phase 4 task 3: source-level guard that the Content Razor feature (Views/Content/*.cshtml)
// stays display/binding only now that ContentController assembles strongly typed view models -
// no application/domain service injection or calls, and no ViewData/ViewBag beyond the standard
// title-metadata exception. Mirrors NoZeroTenantFallbackTests' [CallerFilePath]-based source scan.
public sealed class ContentFeatureSourceGuardTests
{
    private static string GetContentViewsPath([CallerFilePath] string thisFilePath = "")
    {
        var testsDir = Path.GetDirectoryName(thisFilePath)!;
        var srcDir = Path.GetFullPath(Path.Combine(testsDir, ".."));
        return Path.Combine(srcDir, "Web", "Areas", "BackOffice", "Views", "Content");
    }

    private static string[] ContentViewFiles() =>
        Directory.GetFiles(GetContentViewsPath(), "*.cshtml", SearchOption.AllDirectories);

    public static TheoryData<string> BannedSubstrings => new()
    {
        "@inject",
        "GetUserAccesses",
        "RequireApplicationId",
        "ViewBag.",
        ".GetById(",
        "systemTypeServices",
        "applicationServices.",
        "userManagementServices.",
    };

    [Theory]
    [MemberData(nameof(BannedSubstrings))]
    public void ContentViews_DoNotReintroduceDirectServiceCallsOrAccessChecks(string bannedSubstring)
    {
        var files = ContentViewFiles();
        Assert.NotEmpty(files);

        var offending = files
            .Where(f => File.ReadAllText(f).Contains(bannedSubstring))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offending.Count == 0,
            $"Found banned '{bannedSubstring}' in: {string.Join(", ", offending)}");
    }

    [Fact]
    public void ContentViews_OnlyViewDataUsageIsTitleMetadata()
    {
        var viewDataPattern = new Regex("ViewData\\[\"(?<key>[^\"]+)\"\\]");
        var offending = new List<string>();
        foreach (var file in ContentViewFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in viewDataPattern.Matches(text))
            {
                if (match.Groups["key"].Value != "Title")
                    offending.Add($"{Path.GetFileName(file)}: ViewData[\"{match.Groups["key"].Value}\"]");
            }
        }

        Assert.True(offending.Count == 0, $"Found non-title ViewData usage: {string.Join(", ", offending)}");
    }
}
