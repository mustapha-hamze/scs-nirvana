using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Xunit;

namespace Web.Tests;

// Regression coverage for moving Areas/BackOffice/Views/Slider/__SliderJSFunctions.cshtml's
// client logic into wwwroot/BackOffice/js/features/slider.js (Web Phase 4 task 4): the Slider
// entry page must load the feature script exactly once (not the old inline partial), the script
// itself must still be servable as a static asset, and the AJAX endpoint literals it calls must
// still resolve as real routes - pinning the JS asset <-> controller route contract across the
// file move, the same way ContentFeatureScriptTests does for the Content feature.
public sealed class SliderFeatureScriptTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SliderFeatureScriptTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private const string FeatureScriptPath = "/BackOffice/js/features/slider.js";

    [Fact]
    public async Task SliderIndex_AuthenticatedWithTenant_ReferencesFeatureScriptExactlyOnce()
    {
        var email = $"slider-script-ref-{Guid.NewGuid():N}@test.local";
        // SuperAdmin to bypass the Slider access-key gate - this test is about the script tag/asset, not authorization.
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Slider/Index");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(Regex.Matches(html, Regex.Escape(FeatureScriptPath)));
        Assert.DoesNotContain("__SliderJSFunctions", html);
    }

    [Fact]
    public async Task SliderFeatureScript_IsServedAsStaticAsset()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(FeatureScriptPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("function getSliderList(", body);
        Assert.Contains("function createSliderItem(", body);
    }

    // (JS string literal the feature script calls, request path with concrete route values, verb).
    public static TheoryData<string, string, HttpMethod> PinnedAjaxEndpoints => new()
    {
        { "/BackOffice/Slider/Create", "/BackOffice/Slider/Create", HttpMethod.Get },
        { "/BackOffice/Slider/List", "/BackOffice/Slider/List", HttpMethod.Get },
        { "/BackOffice/Slider/SliderItems", "/BackOffice/Slider/SliderItems", HttpMethod.Get },
        { "/BackOffice/Slider/GetSliderItemForm/", "/BackOffice/Slider/GetSliderItemForm/1/0", HttpMethod.Get },
        { "/BackOffice/Slider/GetSliderItemList/", "/BackOffice/Slider/GetSliderItemList/1", HttpMethod.Get },
        { "/BackOffice/Slider/CreateItem", "/BackOffice/Slider/CreateItem", HttpMethod.Post },
        { "/BackOffice/Slider/UploadSliderItemImage", "/BackOffice/Slider/UploadSliderItemImage?sliderId=1&imageFileName=x.jpg", HttpMethod.Post },
        { "/BackOffice/Slider/UpdateItem", "/BackOffice/Slider/UpdateItem", HttpMethod.Post },
        { "/BackOffice/Slider/DeactiveItem", "/BackOffice/Slider/DeactiveItem?sliderItemId=1", HttpMethod.Post },
        { "/BackOffice/Slider/ActiveItem", "/BackOffice/Slider/ActiveItem?sliderItemId=1", HttpMethod.Post },
        { "/BackOffice/Slider/DeleteItem", "/BackOffice/Slider/DeleteItem?sliderItemId=1", HttpMethod.Delete },
    };

    [Theory]
    [MemberData(nameof(PinnedAjaxEndpoints))]
    public async Task SliderFeatureScript_AjaxEndpointLiterals_ResolveAsRealRoutes(string jsUrlLiteral, string requestPath, HttpMethod method)
    {
        var scriptClient = _factory.CreateClient();
        var scriptResponse = await scriptClient.GetAsync(FeatureScriptPath);
        var scriptBody = await scriptResponse.Content.ReadAsStringAsync();
        Assert.Contains(jsUrlLiteral, scriptBody);

        var routeClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var routeResponse = await routeClient.SendAsync(new HttpRequestMessage(method, requestPath));

        // Unauthenticated: proves the route resolves to a real [Authorize] action (redirect to
        // login) rather than 404 (route missing/renamed).
        Assert.Equal(HttpStatusCode.Redirect, routeResponse.StatusCode);
        Assert.Contains("/Login", routeResponse.Headers.Location?.OriginalString ?? string.Empty);
    }
}
