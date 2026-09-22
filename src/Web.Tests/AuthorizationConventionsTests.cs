using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Web.Tests;

// Regression coverage for the Web authorization vocabulary (WebAuthorizationPolicies.SuperAdmin)
// and the explicit-intent audit (HttpGet on ordinary reads, AllowAnonymous on public API
// controllers): proves anonymous public behavior stays anonymous, BackOffice stays authenticated,
// the SuperAdmin policy rejects an authenticated non-SuperAdmin while still admitting a
// SuperAdmin, and a now-GET-only BackOffice read no longer accepts POST.
public sealed class AuthorizationConventionsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthorizationConventionsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_SuperAdminOnlyRoute_AsOrdinaryMember_RedirectsToAccessDenied()
    {
        var email = $"authz-ordinary-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var response = await client.GetAsync("/BackOffice/Account/Users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_SuperAdminOnlyRoute_AsSuperAdmin_ReturnsOk()
    {
        var email = $"authz-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Account/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_SuperAdminOnlyRoute_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/BackOffice/Account/Users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/Category/GetCategories/1/0")]
    [InlineData("/api/Content/GetContent/1/1")]
    [InlineData("/api/Slider/1/GetSlider/1")]
    public async Task PublicApiRoute_ExplicitlyAnonymous_StillResolvesWithoutAuthentication(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ToNowGetOnlyBackOfficeRead_NoLongerExecutes()
    {
        var email = $"authz-verb-{Guid.NewGuid():N}@test.local";
        // SuperAdmin: General is now SuperAdmin-only (see BackOfficeEndpointAuthorizationMatrixTests) -
        // this test is only about the GET-only verb lockdown.
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var getResponse = await client.GetAsync("/BackOffice/General/Tags");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var postResponse = await client.PostAsync("/BackOffice/General/Tags", content: null);
        Assert.NotEqual(HttpStatusCode.OK, postResponse.StatusCode);
    }
}
