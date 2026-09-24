using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Web.Tests;

// Regression coverage for making /Logout (AccountController) and /LogoutApp (ApplicationController)
// POST-only and antiforgery-protected. Both actions clear the caller's selected-application
// session state and sign out in the same unbranched method body, so proving sign-out did not
// happen (via the auth-only /WaitingForApproval probe, which needs no selected application)
// transitively proves the session mutation did not happen either - one call, no branch, both
// effects or neither.
public sealed class LogoutTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public LogoutTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<bool> IsAuthenticatedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/WaitingForApproval");
        if (response.StatusCode == HttpStatusCode.OK)
            return true;
        if (response.StatusCode == HttpStatusCode.Redirect
            && response.Headers.Location?.OriginalString.Contains("/Login", StringComparison.OrdinalIgnoreCase) == true)
            return false;

        throw new InvalidOperationException($"Unexpected auth probe response: {response.StatusCode}");
    }

    [Fact]
    public async Task Get_Logout_DoesNotExecute_AndDoesNotSignOut()
    {
        var email = $"logout-get-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        Assert.True(await IsAuthenticatedAsync(client));

        var getResponse = await client.GetAsync("/Logout");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, getResponse.StatusCode);

        Assert.True(await IsAuthenticatedAsync(client));
    }

    [Fact]
    public async Task Get_LogoutApp_DoesNotExecute_AndDoesNotSignOut()
    {
        var email = $"logoutapp-get-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        Assert.True(await IsAuthenticatedAsync(client));

        var getResponse = await client.GetAsync("/LogoutApp");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, getResponse.StatusCode);

        Assert.True(await IsAuthenticatedAsync(client));
    }

    [Fact]
    public async Task Post_Logout_WithoutAntiforgeryToken_IsRejected_AndDoesNotSignOut()
    {
        var email = $"logout-noaf-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        var postResponse = await client.PostAsync("/Logout", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, postResponse.StatusCode);

        Assert.True(await IsAuthenticatedAsync(client));
    }

    [Fact]
    public async Task Post_Logout_WithValidAntiforgery_SignsOutAndRedirectsHome()
    {
        var email = $"logout-post-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var postResponse = await client.PostAsync("/Logout", content: null);
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal("/", postResponse.Headers.Location?.OriginalString);

        Assert.False(await IsAuthenticatedAsync(client));
    }

    [Fact]
    public async Task Post_LogoutApp_WithValidAntiforgery_SignsOutAndRedirectsToLogin()
    {
        var email = $"logoutapp-post-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var postResponse = await client.PostAsync("/LogoutApp", content: null);
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal("/Login", postResponse.Headers.Location?.OriginalString);

        Assert.False(await IsAuthenticatedAsync(client));
    }
}
