using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Tests;

// Drives the real, unmodified BackOffice login flow over HTTP (GET /Login for the antiforgery
// cookie/token, POST /Login with credentials) so tests exercise the same pipeline a browser does,
// rather than forging an authentication cookie by hand.
internal static class AccountFlowHelper
{
    public static async Task<ApplicationUser> SeedAdminUserAsync(
        TestWebApplicationFactory factory, string email, string password)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "Admin",
            IsAdminUser = true,
            IsApprove = true,
            CreatedDT = DateTime.UtcNow,
            UpdatedDT = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Failed to seed test user: " + string.Join(", ", result.Errors.Select(e => e.Description)));

        return user;
    }

    // Logs in over real HTTP and returns a client whose default "X-CSRF-TOKEN" header carries a
    // valid antiforgery request token pairing the cookie the login page issued - the same
    // convention the BackOffice's own AJAX calls use (see _Layout.cshtml's $.ajaxSetup).
    public static async Task<HttpClient> LoginAsync(TestWebApplicationFactory factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var loginPage = await client.GetAsync("/Login");
        var anonymousToken = ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", anonymousToken);

        var loginResponse = await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EmailAddress"] = email,
            ["Password"] = password
        }));

        if (loginResponse.StatusCode != System.Net.HttpStatusCode.Found)
            throw new InvalidOperationException(
                $"Login did not redirect as expected; got {loginResponse.StatusCode}: {await loginResponse.Content.ReadAsStringAsync()}");

        // The antiforgery token embeds the caller's identity, so the token minted above (while
        // still anonymous) is rejected now that the client is authenticated - re-render it from
        // an authenticated request, exactly as a real authenticated page would.
        await RefreshAntiForgeryTokenAsync(client);

        return client;
    }

    public static async Task RefreshAntiForgeryTokenAsync(HttpClient client)
    {
        var page = await client.GetAsync("/Login");
        var token = ExtractAntiForgeryToken(await page.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
    }

    public static string ExtractAntiForgeryToken(string html)
    {
        var tag = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase);
        if (!tag.Success)
            throw new InvalidOperationException("Antiforgery token field not found in response HTML.");

        var value = Regex.Match(tag.Value, "value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        if (!value.Success)
            throw new InvalidOperationException("Antiforgery token value not found in input tag.");

        return value.Groups[1].Value;
    }
}
