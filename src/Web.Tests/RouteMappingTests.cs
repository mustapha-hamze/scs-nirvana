using System.Net;
using System.Net.Http;
using Xunit;

namespace Web.Tests;

// Pins the routes that matter before/after Program.cs's UseEndpoints -> direct MapControllerRoute
// /MapControllers/MapRazorPages migration: the new /healthz liveness probe, a representative
// BackOffice (attribute-routed area) action, and every public API controller's route. A regression
// in route selection during that migration shows up here as 404 instead of the status asserted.
public sealed class RouteMappingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RouteMappingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthCheck_IsAnonymousAndHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BackOfficeHome_Unauthenticated_RedirectsToLogin_RouteResolves()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/BackOffice/Home/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [Fact]
    public async Task BackOfficeHome_AuthenticatedWithTenant_ReturnsOk()
    {
        var email = $"route-backoffice-{System.Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Home/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/Category/GetCategories/1/0")]
    [InlineData("/api/Content/GetContent/1/1")]
    [InlineData("/api/Content/GetContentByTypeId/1/1")]
    [InlineData("/api/Content/GetContentByTypeId/1/1/1")]
    [InlineData("/api/Slider/1/GetSlider/1")]
    public async Task PublicApiRoute_Resolves(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
