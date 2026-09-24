using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Xunit;

namespace Web.Tests;

// Regression coverage for moving Areas/BackOffice/Views/Content/__ContentJSFunctions.cshtml's
// client logic into wwwroot/BackOffice/js/features/content.js: the Content entry page must load
// the feature script exactly once (not the old inline partial), the script itself must still be
// servable as a static asset, and the AJAX endpoint literals it calls must still resolve as real
// routes - pinning the JS asset <-> controller route contract across the file move.
public sealed class ContentFeatureScriptTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContentFeatureScriptTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private const string FeatureScriptPath = "/BackOffice/js/features/content.js";

    [Fact]
    public async Task ContentIndex_AuthenticatedWithTenant_ReferencesFeatureScriptExactlyOnce()
    {
        var email = $"content-script-ref-{Guid.NewGuid():N}@test.local";
        // SuperAdmin to bypass the Content access-key gate - this test is about the script tag/asset, not authorization.
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Content/Index/1000");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(Regex.Matches(html, Regex.Escape(FeatureScriptPath)));
        Assert.DoesNotContain("__ContentJSFunctions", html);
    }

    [Fact]
    public async Task ContentFeatureScript_IsServedAsStaticAsset()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(FeatureScriptPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("function findContents(", body);
        Assert.Contains("function saveBody(", body);
    }

    // (JS string literal the feature script calls, request path with concrete route values, verb).
    // Asserting the literal is still present in the shipped script - not just assumed - means a
    // future edit to either side (the JS call site or the controller route) that breaks the pair
    // fails this test instead of only failing silently in the browser.
    public static TheoryData<string, string, HttpMethod> PinnedAjaxEndpoints => new()
    {
        { "/BackOffice/Content/ContentList/", "/BackOffice/Content/ContentList/1000", HttpMethod.Get },
        { "/BackOffice/Content/CreateContentSection/", "/BackOffice/Content/CreateContentSection/1/1", HttpMethod.Get },
        { "/BackOffice/Content/SaveRelation/", "/BackOffice/Content/SaveRelation/Category/1", HttpMethod.Post },
        { "/BackOffice/Content/SaveSection", "/BackOffice/Content/SaveSection", HttpMethod.Post },
        { "/BackOffice/Content/UpdateSectionsLayoutOrder", "/BackOffice/Content/UpdateSectionsLayoutOrder", HttpMethod.Post },
        { "/BackOffice/Content/DeleteSection/", "/BackOffice/Content/DeleteSection/1", HttpMethod.Delete },
        { "/BackOffice/Content/SaveContentMetadata", "/BackOffice/Content/SaveContentMetadata", HttpMethod.Post },
        { "/BackOffice/Content/UploadContentImage", "/BackOffice/Content/UploadContentImage", HttpMethod.Post },
        { "/BackOffice/Content/ContentRelations/", "/BackOffice/Content/ContentRelations/1", HttpMethod.Get },
        { "/BackOffice/Content/ContentMetadata/", "/BackOffice/Content/ContentMetadata/1", HttpMethod.Get },
    };

    [Theory]
    [MemberData(nameof(PinnedAjaxEndpoints))]
    public async Task ContentFeatureScript_AjaxEndpointLiterals_ResolveAsRealRoutes(string jsUrlLiteral, string requestPath, HttpMethod method)
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
