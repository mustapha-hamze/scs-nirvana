using System;
using System.Net;
using System.Net.Http;
using Domains.Entities.ContentManagement;
using Domains.Entities.CustomModule;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// Real HTTP integration coverage for RequireAccessAttribute/AccessKeyAuthorizer (Web Phase 3
// task 3) against the two BackOffice features with the richest access-key contract: Content and
// Slider. One read + one unsafe mutation per feature, each proven across: unauthenticated,
// missing tenant selection (retains RequireTenantContextFilter's existing redirect), no matching
// permission (forbidden, no mutation), a similar/prefix permission (still denied - proves exact-
// token comparison, not Contains), the exact permission (allowed), and SuperAdmin (always
// allowed).
public sealed class AccessKeyAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AccessKeyAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SeedContentAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var content = new Content { ApplicationId = applicationId, TypeId = 1000, Title = "Access-key content", PublishDt = DateTime.UtcNow };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        return content.Id;
    }

    private async Task<bool> ContentExistsAsync(int contentId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Contents.FindAsync(contentId) is not null;
    }

    private async Task<int> SeedSliderItemAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slider = new Slider { ApplicationId = applicationId, Title = "Access-key slider" };
        context.Sliders.Add(slider);
        await context.SaveChangesAsync();
        var item = new SliderItem { SliderId = slider.Id, Title = "Item", ImageFileName = "img.jpg" };
        context.SliderItems.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }

    private async Task<bool> SliderItemExistsAsync(int sliderItemId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.SliderItems.FindAsync(sliderItemId) is not null;
    }

    // ---- Content: read = ContentList, unsafe mutation = DeleteContent ----

    [Fact]
    public async Task Content_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task Content_MissingTenantSelection_RetainsExistingSelectAppRedirect()
    {
        var email = $"access-content-notenant-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/BackOffice/Application/SelectApp", response.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task Content_ValidTenantNoMatchingPermission_ReadIsForbidden()
    {
        var email = $"access-content-noperm-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Content_SimilarPrefixPermission_ReadIsStillDenied()
    {
        var email = $"access-content-prefix-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        // A key that shares the Content module key as a strict prefix, but isn't it - proves
        // exact-token comparison server-side (Contains(...) would wrongly satisfy this).
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, "CMS1000_10011");

        var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Content_ExactPermission_ReadIsAllowed()
    {
        var email = $"access-content-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Content.Module);

        var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Content_SuperAdmin_ReadIsAlwaysAllowed()
    {
        var email = $"access-content-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Content_ValidTenantNoMatchingPermission_DeleteIsForbiddenAndDoesNotMutate()
    {
        var email = $"access-content-delete-noperm-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var contentId = await SeedContentAsync(applicationId);

        var response = await client.DeleteAsync($"/BackOffice/Content/DeleteContent/{contentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await ContentExistsAsync(contentId));
    }

    [Fact]
    public async Task Content_ExactPermission_DeleteSucceedsAndMutates()
    {
        var email = $"access-content-delete-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var contentId = await SeedContentAsync(applicationId);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Content.Delete);

        var response = await client.DeleteAsync($"/BackOffice/Content/DeleteContent/{contentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await ContentExistsAsync(contentId));
    }

    // ---- Slider: read = List, unsafe mutation = DeleteItem ----

    [Fact]
    public async Task Slider_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/BackOffice/Slider/List");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task Slider_MissingTenantSelection_RetainsExistingSelectAppRedirect()
    {
        var email = $"access-slider-notenant-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var response = await client.GetAsync("/BackOffice/Slider/List");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/BackOffice/Application/SelectApp", response.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task Slider_ValidTenantNoMatchingPermission_ReadIsForbidden()
    {
        var email = $"access-slider-noperm-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Slider/List");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Slider_SimilarPrefixPermission_ReadIsStillDenied()
    {
        var email = $"access-slider-prefix-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, "SCM3000_10011");

        var response = await client.GetAsync("/BackOffice/Slider/List");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Slider_ExactPermission_ReadIsAllowed()
    {
        var email = $"access-slider-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Slider.Module);

        var response = await client.GetAsync("/BackOffice/Slider/List");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Slider_SuperAdmin_ReadIsAlwaysAllowed()
    {
        var email = $"access-slider-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Slider/List");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Slider_ValidTenantNoMatchingPermission_DeleteItemIsForbiddenAndDoesNotMutate()
    {
        var email = $"access-slider-delete-noperm-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var sliderItemId = await SeedSliderItemAsync(applicationId);

        var response = await client.DeleteAsync($"/BackOffice/Slider/DeleteItem?sliderItemId={sliderItemId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await SliderItemExistsAsync(sliderItemId));
    }

    [Fact]
    public async Task Slider_DeleteItem_WithoutAntiforgeryToken_IsRejected()
    {
        var email = $"access-slider-delete-noaf-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var sliderItemId = await SeedSliderItemAsync(applicationId);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Slider.DeleteItem);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        var response = await client.DeleteAsync($"/BackOffice/Slider/DeleteItem?sliderItemId={sliderItemId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await SliderItemExistsAsync(sliderItemId));
    }

    [Fact]
    public async Task Slider_ExactPermission_DeleteItemSucceedsAndMutates()
    {
        var email = $"access-slider-delete-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var sliderItemId = await SeedSliderItemAsync(applicationId);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Slider.DeleteItem);

        var response = await client.DeleteAsync($"/BackOffice/Slider/DeleteItem?sliderItemId={sliderItemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await SliderItemExistsAsync(sliderItemId));
    }
}
