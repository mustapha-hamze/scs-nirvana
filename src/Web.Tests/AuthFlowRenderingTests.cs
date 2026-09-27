using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// Renders each state of the BackOffice sign-in flow over real HTTP: the login form (with its
// unchanged field names and antiforgery token), its credential and field errors, pending approval,
// the no-workspace picker, and the public error page. Script behaviour lives in js/auth.test.mjs.
public sealed class AuthFlowRenderingTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Password = "CorrectHorseBattery12";
    private readonly TestWebApplicationFactory _factory;

    public AuthFlowRenderingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_Get_RendersFormWithOriginalFieldsAndNoError()
    {
        var body = await _factory.CreateClient().GetStringAsync("/Login");

        Assert.Contains("Sign in to the BackOffice", body);
        Assert.Contains("name=\"__RequestVerificationToken\"", body);
        Assert.Contains("id=\"emailaddress\"", body);
        Assert.Contains("name=\"EmailAddress\"", body);
        Assert.Contains("id=\"password\"", body);
        Assert.Contains("name=\"Password\"", body);
        Assert.Contains("id=\"checkbox-signin\"", body);
        Assert.Contains("data-scs-password-toggle", body);
        Assert.Contains("aria-controls=\"password\"", body);
        Assert.Contains("/BackOffice/js/features/auth.js", body);
        Assert.DoesNotContain("Sign-in failed:", body);
        Assert.DoesNotContain("aria-invalid", body);
    }

    [Fact]
    public async Task Login_WrongPassword_ShowsCredentialAlertAndKeepsEmail()
    {
        var email = $"login-fail-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, Password);

        var (response, body) = await PostLoginAsync(email, "WrongPassword99");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("role=\"alert\"", body);
        Assert.Contains("Sign-in failed:", body);
        Assert.Contains($"value=\"{email}\"", body);
    }

    [Fact]
    public async Task Login_InvalidEmail_MarksFieldInvalidAndDescribesError()
    {
        var (response, body) = await PostLoginAsync("not-an-email", "whatever");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aria-invalid=\"true\"", body);
        Assert.Contains("aria-describedby=\"emailaddress-error\"", body);
        Assert.Contains("is-invalid", body);
        Assert.Contains("field-validation-error", body);
    }

    [Fact]
    public async Task UnapprovedUser_IsSentToWaitingForApproval_WithCheckAgainAndSignOut()
    {
        var email = $"pending-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, Password);
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user!.IsApprove = false;
            await userManager.UpdateAsync(user);
        }
        var client = await AccountFlowHelper.LoginAsync(_factory, email, Password);

        var selectApp = await client.GetAsync("/BackOffice/Application/SelectApp");
        Assert.Equal(HttpStatusCode.Redirect, selectApp.StatusCode);
        Assert.Equal("/WaitingForApproval", selectApp.Headers.Location?.OriginalString);

        var body = await client.GetStringAsync("/WaitingForApproval");
        Assert.Contains("Your account is waiting for approval", body);
        Assert.Contains("href=\"/BackOffice/Application/SelectApp\"", body);
        Assert.Contains("action=\"/Logout\"", body);
        Assert.Contains("name=\"__RequestVerificationToken\"", body);
    }

    [Fact]
    public async Task ApprovedUserWithoutMemberships_SeesNoWorkspaceStateWithSignOut()
    {
        var email = $"no-workspace-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, Password);
        var client = await AccountFlowHelper.LoginAsync(_factory, email, Password);

        var body = await client.GetStringAsync("/BackOffice/Application/SelectApp");

        Assert.Contains("No workspaces available yet", body);
        Assert.Contains("action=\"/Logout\"", body);
    }

    [Fact]
    public async Task PublicErrorPage_ExplainsAndOffersNextActionWithReference()
    {
        var body = await _factory.CreateClient().GetStringAsync("/Home/Error");

        Assert.Contains("Something went wrong", body);
        Assert.Contains("href=\"/BackOffice/Application/SelectApp\"", body);
        Assert.Contains("quote reference <code>", body);
        Assert.Contains("/BackOffice/css/scs-admin.css?v=", body);
    }

    private async Task<(HttpResponseMessage Response, string Body)> PostLoginAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = AccountFlowHelper.ExtractAntiForgeryToken(await client.GetStringAsync("/Login"));

        var response = await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["EmailAddress"] = email,
            ["Password"] = password
        }));
        return (response, await response.Content.ReadAsStringAsync());
    }
}
