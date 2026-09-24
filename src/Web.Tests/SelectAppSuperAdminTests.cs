using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// A SuperAdmin may view and select every active, non-deleted application without a
// UserInApplication membership row of their own (ApplicationController.SelectApp/SelectAppToEnter,
// TenantAccessGuard.HasAccessAsync). Ordinary users remain limited to their own active
// memberships. Real HTTP, with Identity/antiforgery/session all exercised for real - not
// reflection alone.
public sealed class SelectAppSuperAdminTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SelectAppSuperAdminTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SeedApplicationAsync(string title, bool isActive, bool isDeleted = false)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var application = new Domains.Entities.General.Application { Title = title, IsActive = isActive, IsDeleted = isDeleted };
        context.Applications.Add(application);
        await context.SaveChangesAsync();
        return application.Id;
    }

    private async Task AddMembershipAsync(string userId, int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserInApplications.Add(new UserInApplication { UserId = userId, ApplicationId = applicationId, IsActive = true });
        await context.SaveChangesAsync();
    }

    // (a) A membership-less SuperAdmin's SelectApp GET shows all and only active applications.
    [Fact]
    public async Task SelectApp_SuperAdminWithNoMembership_ShowsOnlyActiveApplications()
    {
        var email = $"superadmin-selectapp-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");

        var activeTitle = $"Active {Guid.NewGuid():N}";
        var inactiveTitle = $"Inactive {Guid.NewGuid():N}";
        var deletedTitle = $"Deleted {Guid.NewGuid():N}";
        await SeedApplicationAsync(activeTitle, isActive: true);
        await SeedApplicationAsync(inactiveTitle, isActive: false);
        await SeedApplicationAsync(deletedTitle, isActive: true, isDeleted: true);

        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var response = await client.GetAsync("/BackOffice/Application/SelectApp");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(activeTitle, body);
        Assert.DoesNotContain(inactiveTitle, body);
        Assert.DoesNotContain(deletedTitle, body);
    }

    // (b) That SuperAdmin can then POST-select the active application and reach a
    // tenant-protected BackOffice endpoint (BaseController's RequireTenantContextFilter).
    [Fact]
    public async Task SelectAppToEnter_SuperAdminWithNoMembership_SelectsActiveApplication_AndReachesTenantProtectedEndpoint()
    {
        var email = $"superadmin-select-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await SeedApplicationAsync($"App {Guid.NewGuid():N}", isActive: true);

        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var selectResponse = await client.PostAsync($"/BackOffice/Application/SelectAppToEnter/{applicationId}", content: null);
        Assert.Equal(HttpStatusCode.Redirect, selectResponse.StatusCode);
        Assert.Equal("/BackOffice/Home/Index", selectResponse.Headers.Location?.OriginalString);

        var protectedResponse = await client.GetAsync("/BackOffice/Home/Index");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    // (c) SuperAdmin cannot select an inactive (or deleted) application, and no valid tenant
    // context results - a protected endpoint requested afterward still bounces to SelectApp.
    [Fact]
    public async Task SelectAppToEnter_SuperAdminInactiveApplication_IsDenied_AndNoTenantContextResults()
    {
        var email = $"superadmin-inactive-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var inactiveApplicationId = await SeedApplicationAsync($"Inactive {Guid.NewGuid():N}", isActive: false);

        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var selectResponse = await client.PostAsync($"/BackOffice/Application/SelectAppToEnter/{inactiveApplicationId}", content: null);
        Assert.Equal(HttpStatusCode.Redirect, selectResponse.StatusCode);
        Assert.Equal("/BackOffice/Application/SelectApp", selectResponse.Headers.Location?.OriginalString);

        // No tenant context resulted from the rejected selection - a protected endpoint still
        // bounces back to SelectApp instead of rendering.
        var protectedResponse = await client.GetAsync("/BackOffice/Home/Index");
        Assert.Equal(HttpStatusCode.Redirect, protectedResponse.StatusCode);
        Assert.Equal("/BackOffice/Application/SelectApp", protectedResponse.Headers.Location?.OriginalString);
    }

    // (d) An ordinary member remains limited to their own memberships and cannot select another
    // active application they don't belong to.
    [Fact]
    public async Task SelectAppToEnter_NormalMember_CannotSelectAnotherActiveApplicationWithoutMembership()
    {
        var email = $"member-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");

        var ownApplicationId = await SeedApplicationAsync($"Own {Guid.NewGuid():N}", isActive: true);
        await AddMembershipAsync(user.Id, ownApplicationId);

        var otherApplicationId = await SeedApplicationAsync($"Other {Guid.NewGuid():N}", isActive: true);

        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var selectOtherResponse = await client.PostAsync($"/BackOffice/Application/SelectAppToEnter/{otherApplicationId}", content: null);
        Assert.Equal(HttpStatusCode.Redirect, selectOtherResponse.StatusCode);
        Assert.Equal("/BackOffice/Application/SelectApp", selectOtherResponse.Headers.Location?.OriginalString);

        // The failed selection must not have set any tenant context - selecting the member's own
        // application afterward still has to work from a clean slate.
        var selectOwnResponse = await client.PostAsync($"/BackOffice/Application/SelectAppToEnter/{ownApplicationId}", content: null);
        Assert.Equal(HttpStatusCode.Redirect, selectOwnResponse.StatusCode);
        Assert.Equal("/BackOffice/Home/Index", selectOwnResponse.Headers.Location?.OriginalString);
    }
}
