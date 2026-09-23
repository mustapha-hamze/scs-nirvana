using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Web.Tests;

// Web Phase 5 task 3: the shared BackOffice sidebar/layout is a defect surface, not just markup -
// a visible link with no live route behind it is a 404 a real user can click into. This pins every
// remaining literal (non-Razor-interpolated) sidebar href to a real, working route over HTTP as
// SuperAdmin (which the AccessKeyAuthorizer already treats as always-allowed, so no per-feature
// access-key grant is needed here), and pins the removed dead references' absence so they can't
// silently come back.
public sealed class BackOfficeSharedNavigationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public BackOfficeSharedNavigationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string SharedViewsDir([CallerFilePath] string thisFilePath = "")
    {
        var testsDir = Path.GetDirectoryName(thisFilePath)!;
        var srcDir = Path.GetFullPath(Path.Combine(testsDir, ".."));
        return Path.Combine(srcDir, "Web", "Areas", "BackOffice", "Views", "Shared");
    }

    private static string[] SideBarFiles() =>
        Directory.GetFiles(SharedViewsDir(), "_SideBar*.cshtml", SearchOption.TopDirectoryOnly);

    private static readonly string[] LayoutAssetFiles =
    {
        "_Layout.cshtml",
        "_JSAssets.cshtml",
    };

    // Every href="/BackOffice/..." in the sidebar partials that is a plain string literal, not a
    // Razor-interpolated one (e.g. href="/BackOffice/Content/Index/@item.Id") - those are covered
    // by ContentControllerRouteRegressionTests/RouteMappingTests, not this static link inventory.
    public static IEnumerable<string> LiteralSideBarLinks()
    {
        var pattern = new Regex("href=\"(?<url>/BackOffice/[^\"@]*)\"");
        foreach (var file in SideBarFiles())
        {
            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
                yield return match.Groups["url"].Value;
        }
    }

    public static TheoryData<string> LiteralSideBarLinksData()
    {
        var data = new TheoryData<string>();
        foreach (var url in LiteralSideBarLinks().Distinct())
            data.Add(url);
        return data;
    }

    [Theory]
    [MemberData(nameof(LiteralSideBarLinksData))]
    public async Task SideBarLink_ResolvesToALiveRoute(string url)
    {
        var email = $"sidebar-link-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void SideBarLinks_AreNotEmpty()
    {
        // Guards the theory above against silently discovering zero links (e.g. a regex/path typo)
        // and passing vacuously.
        Assert.True(LiteralSideBarLinks().Distinct().Count() >= 10);
    }

    [Fact]
    public void SideBarAndLayoutAssets_DoNotReferenceRemovedDeadLinksOrHelpers()
    {
        var removedSubstrings = new[]
        {
            "General/Zones",
            "General/Currencies",
            "RefereshCacheDb",
            "refreshCacheDB",
        };

        var files = SideBarFiles()
            .Concat(LayoutAssetFiles.Select(f => Path.Combine(SharedViewsDir(), f)))
            .ToList();

        var offending = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var substring in removedSubstrings)
            {
                if (text.Contains(substring))
                    offending.Add($"{Path.GetFileName(file)}: {substring}");
            }
        }

        Assert.True(offending.Count == 0, $"Found reintroduced dead reference(s): {string.Join(", ", offending)}");
    }
}
