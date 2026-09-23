using System.Net;
using System.Net.Http;
using Xunit;

namespace Web.Tests;

// Web Phase 4 fix (finding 2): proves BackOffice sidebar link visibility is aligned with what its
// destination action would actually do - a Content/Slider module link renders only when the exact
// module key (or SuperAdmin) would also let the linked Index action succeed, so a link never leads
// to a 403. Each scenario checks both halves together: the rendered sidebar (via
// /BackOffice/Home/Index, an Auth.Authenticated page every tenant member can reach) and the real
// destination route, from the same granted-keys setup.
public sealed class BackOfficeNavigationAuthorizationAlignmentTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public BackOfficeNavigationAuthorizationAlignmentTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, string Body)> RenderSidebarAsync(HttpClient client)
    {
        var response = await client.GetAsync("/BackOffice/Home/Index");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (client, await response.Content.ReadAsStringAsync());
    }

    // ---- Content ----

    [Fact]
    public async Task ContentLink_ExactModuleKey_ShowsLinkAndIndexSucceeds()
    {
        var email = $"nav-content-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Content.Module);

        var (_, body) = await RenderSidebarAsync(client);
        Assert.Contains("id=\"liCMSContent\"", body);

        var indexResponse = await client.GetAsync("/BackOffice/Content/Index/1000");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
    }

    [Fact]
    public async Task ContentLink_GranularKeyOnly_HidesLinkAndIndexIsForbidden()
    {
        var email = $"nav-content-granular-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        // A real sub-action key, never issued alongside the module key it doesn't imply.
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Content.PreviewRelations);

        var (_, body) = await RenderSidebarAsync(client);
        Assert.DoesNotContain("id=\"liCMSContent\"", body);

        var indexResponse = await client.GetAsync("/BackOffice/Content/Index/1000");
        Assert.Equal(HttpStatusCode.Forbidden, indexResponse.StatusCode);
    }

    [Fact]
    public async Task ContentLink_SimilarPrefixKey_HidesLinkAndIndexIsForbidden()
    {
        var email = $"nav-content-prefix-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        // Shares "CMS1000_1001" as a string prefix but is a different key.
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, "CMS1000_10011");

        var (_, body) = await RenderSidebarAsync(client);
        Assert.DoesNotContain("id=\"liCMSContent\"", body);

        var indexResponse = await client.GetAsync("/BackOffice/Content/Index/1000");
        Assert.Equal(HttpStatusCode.Forbidden, indexResponse.StatusCode);
    }

    [Fact]
    public async Task ContentLink_SuperAdmin_ShowsLinkAndIndexSucceeds()
    {
        var email = $"nav-content-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var (_, body) = await RenderSidebarAsync(client);
        Assert.Contains("id=\"liCMSContent\"", body);

        var indexResponse = await client.GetAsync("/BackOffice/Content/Index/1000");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
    }

    // ---- Slider ----

    [Fact]
    public async Task SliderLink_ExactModuleKey_ShowsLinkAndIndexSucceeds()
    {
        var email = $"nav-slider-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Slider.Module);

        var (_, body) = await RenderSidebarAsync(client);
        Assert.Contains("id=\"liSCMSlider\"", body);

        var indexResponse = await client.GetAsync("/BackOffice/Slider/Index");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
    }

    [Fact]
    public async Task SliderLink_GranularKeyOnly_HidesLinkAndIndexIsForbidden()
    {
        var email = $"nav-slider-granular-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Slider.AccessItems);

        var (_, body) = await RenderSidebarAsync(client);
        Assert.DoesNotContain("id=\"liSCMSlider\"", body);

        var indexResponse = await client.GetAsync("/BackOffice/Slider/Index");
        Assert.Equal(HttpStatusCode.Forbidden, indexResponse.StatusCode);
    }

    [Fact]
    public async Task SliderLink_SimilarPrefixKey_HidesLinkAndIndexIsForbidden()
    {
        var email = $"nav-slider-prefix-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        // Shares "SCM3000_1001" as a string prefix but is a different key.
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, "SCM3000_10011");

        var (_, body) = await RenderSidebarAsync(client);
        Assert.DoesNotContain("id=\"liSCMSlider\"", body);

        var indexResponse = await client.GetAsync("/BackOffice/Slider/Index");
        Assert.Equal(HttpStatusCode.Forbidden, indexResponse.StatusCode);
    }

    [Fact]
    public async Task SliderLink_SuperAdmin_ShowsLinkAndIndexSucceeds()
    {
        var email = $"nav-slider-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var (_, body) = await RenderSidebarAsync(client);
        Assert.Contains("id=\"liSCMSlider\"", body);

        var indexResponse = await client.GetAsync("/BackOffice/Slider/Index");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
    }
}
