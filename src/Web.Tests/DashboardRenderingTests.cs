using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Xunit;

namespace Web.Tests;

// Rendered-page coverage for the BackOffice Dashboard (Views/Home/Index.cshtml): module cards and
// quick actions follow the exact access keys their destinations require, icons come from the
// shipped unicons font, and Recent activity stays an honest empty state.
public sealed class DashboardRenderingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DashboardRenderingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CategoryMember_SeesOnlyTheCategoriesModule()
    {
        var email = $"dash-cms-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Category.Module);

        var body = await GetDashboardAsync(client);

        // Each card follows its destination's exact key: a Category key alone never shows Content/Schemas.
        Assert.DoesNotContain(">Content</h3>", body);
        Assert.Contains(">Categories</h3>", body);
        Assert.DoesNotContain(">Schemas</h3>", body);
        Assert.DoesNotContain(">Sliders</h3>", body);
        Assert.DoesNotContain(">Users &amp; roles</h3>", body);
        Assert.Contains("href=\"/BackOffice/Category/Index\" class=\"btn btn-primary", body);
        Assert.DoesNotContain("No modules assigned yet", body);
        Assert.Contains("Activity history isn't tracked yet", body);
    }

    [Fact]
    public async Task ScmMember_SeesOnlySlidersModule()
    {
        var email = $"dash-scm-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Slider.Module);

        var body = await GetDashboardAsync(client);

        Assert.Contains(">Sliders</h3>", body);
        Assert.DoesNotContain(">Content</h3>", body);
        Assert.Contains("href=\"/BackOffice/Slider/Index\" class=\"btn btn-primary", body);
    }

    [Fact]
    public async Task SuperAdmin_SeesAdministrationModulesAndShortcuts()
    {
        var email = $"dash-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var body = await GetDashboardAsync(client);

        Assert.Contains(">Users &amp; roles</h3>", body);
        Assert.Contains(">Settings</h3>", body);
        Assert.Contains("href=\"/BackOffice/General/SystemTypes\"", body);
        Assert.Single(Regex.Matches(body, "class=\"btn btn-primary w-100 scs-dash-primary\""));
    }

    [Fact]
    public async Task MemberWithoutModules_SeesGuidedNoAccessState()
    {
        var email = $"dash-none-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var body = await GetDashboardAsync(client);

        Assert.Contains("No modules assigned yet", body);
        Assert.DoesNotContain("scs-dash-primary", body);
        Assert.DoesNotContain("scs-dash-card", body);
    }

    [Fact]
    public async Task DashboardIcons_AllExistInShippedIconFont()
    {
        var email = $"dash-icons-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var body = await GetDashboardAsync(client);
        var iconCss = await (await client.GetAsync("/BackOffice/css/icons.min.css")).Content.ReadAsStringAsync();

        var main = body[body.IndexOf("<main", StringComparison.Ordinal)..];
        var icons = Regex.Matches(main, @"\buil-[a-z0-9-]+").Select(m => m.Value).Distinct().ToList();
        Assert.NotEmpty(icons);
        Assert.All(icons, icon => Assert.Contains($".{icon}:before", iconCss));
    }

    private static async Task<string> GetDashboardAsync(HttpClient client)
    {
        var response = await client.GetAsync("/BackOffice/Home/Index");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
