using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Domains.Entities.General;
using Infrastructure.Data;
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

    // Same admin-user seeding as SeedAdminUserAsync, plus the SuperAdmin role that gates every
    // [Authorize(Roles = "SuperAdmin")] BackOffice action (user/role/membership/attachment
    // administration).
    public static async Task<ApplicationUser> SeedSuperAdminUserAsync(
        TestWebApplicationFactory factory, string email, string password)
    {
        var seeded = await SeedAdminUserAsync(factory, email, password);

        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync("SuperAdmin"))
            await roleManager.CreateAsync(new IdentityRole("SuperAdmin"));

        // Re-fetch through this scope's own UserManager/DbContext instance rather than reusing
        // the entity SeedAdminUserAsync's own (already-disposed) scope created and tracked.
        var user = await userManager.FindByIdAsync(seeded.Id)
            ?? throw new InvalidOperationException($"Seeded user '{seeded.Id}' was not found.");
        await userManager.AddToRoleAsync(user, "SuperAdmin");

        return user;
    }

    // Establishes the session-backed tenant context RequireTenantContextFilter requires on every
    // BaseController action: seeds an active Application and an active UserInApplication
    // membership for the caller, then drives the real SelectAppToEnter flow over HTTP exactly as
    // a browser would after picking a tenant, so client's session cookie ends up carrying it.
    public static async Task<int> SelectApplicationAsync(TestWebApplicationFactory factory, HttpClient client, ApplicationUser user)
    {
        int applicationId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var application = new Domains.Entities.General.Application { Title = $"Test App {Guid.NewGuid():N}", IsActive = true };
            context.Applications.Add(application);
            await context.SaveChangesAsync();
            applicationId = application.Id;

            context.UserInApplications.Add(new UserInApplication { UserId = user.Id, ApplicationId = applicationId, IsActive = true });
            await context.SaveChangesAsync();
        }

        var response = await client.PostAsync($"/BackOffice/Application/SelectAppToEnter/{applicationId}", content: null);
        if (response.StatusCode != HttpStatusCode.Redirect || response.Headers.Location?.OriginalString != "/BackOffice/Home/Index")
            throw new InvalidOperationException(
                $"Failed to select application context; got {response.StatusCode} -> {response.Headers.Location}");

        return applicationId;
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

    // Grants the caller's persisted, tenant-scoped access-key string for applicationId, exactly
    // as AccountController.SetAccessForUser (SuperAdmin-only) would - through the real
    // IUserManagementServices.SetUserAccesses port, not a raw DbContext write, so this stays a
    // faithful stand-in for how the app itself grants access.
    public static async Task GrantAccessAsync(TestWebApplicationFactory factory, ApplicationUser user, int applicationId, string accesses)
    {
        using var scope = factory.Services.CreateScope();
        var userManagementServices = scope.ServiceProvider.GetRequiredService<Application.UseCases.UserManagementServices.IUserManagementServices>();
        await userManagementServices.SetUserAccesses(accesses, user.Id, applicationId);
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
