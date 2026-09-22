using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Domains.Entities.CustomModule;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Authorization;
using Xunit;

namespace Web.Tests;

// Web Phase 4 task 4: rendering/binding and end-to-end flow coverage for the Slider feature pilot
// - the strongly typed view models (Web/Areas/BackOffice/Features/Slider/ViewModels) and the JS
// asset move. Proves List/Create/item-form/item-list visibility still gates on the same access
// keys the old accesses.Contains checks used, now resolved once per request via AccessKeyAuthorizer,
// and that create/update flows still enforce tenant permission and the global antiforgery token.
public sealed class SliderFeatureViewModelRenderingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SliderFeatureViewModelRenderingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Infrastructure.Identity.ApplicationUser User, int ApplicationId)> SeedTenantMemberAsync(string keys)
    {
        var email = $"slider-render-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        if (!string.IsNullOrEmpty(keys))
            await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, keys);
        return (client, user, applicationId);
    }

    private async Task<int> SeedSliderAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slider = new Slider { ApplicationId = applicationId, Title = "Rendering slider" };
        context.Sliders.Add(slider);
        await context.SaveChangesAsync();
        return slider.Id;
    }

    private async Task<int> SeedSliderItemAsync(int sliderId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = new SliderItem { SliderId = sliderId, Title = "Item", ImageFileName = "img.jpg" };
        context.SliderItems.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }

    private async Task<int> SliderCountAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Sliders.CountAsync(s => s.ApplicationId == applicationId);
    }

    private async Task<int> SliderItemCountAsync(int sliderId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.SliderItems.CountAsync(i => i.SliderId == sliderId);
    }

    private async Task<string> SliderItemTitleAsync(int sliderItemId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await context.SliderItems.FindAsync(sliderItemId);
        return item!.Title;
    }

    // ---- List ----

    [Fact]
    public async Task List_WithAccessItemsKey_ShowsManageItemsLink()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Slider.Module, AccessKeys.Slider.AccessItems));
        var sliderId = await SeedSliderAsync(applicationId);

        var response = await client.GetAsync("/BackOffice/Slider/List");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"sliderItems('{sliderId}')", body);
    }

    [Fact]
    public async Task List_WithoutAccessItemsKey_HidesManageItemsLink()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Slider.Module);
        var sliderId = await SeedSliderAsync(applicationId);

        var response = await client.GetAsync("/BackOffice/Slider/List");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain($"sliderItems('{sliderId}')", body);
    }

    // ---- Create ----

    [Fact]
    public async Task CreatePage_WithSaveKey_ShowsCreateButtonAndApplicationId()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Slider.Add, AccessKeys.Slider.Save));

        var response = await client.GetAsync("/BackOffice/Slider/Create");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"__btnCreateSlider__\"", body);
        Assert.Contains($"id=\"ApplicationId\" value=\"{applicationId}\"", body);
    }

    [Fact]
    public async Task CreatePage_WithoutSaveKey_HidesCreateButton()
    {
        var (client, _, _) = await SeedTenantMemberAsync(AccessKeys.Slider.Add);

        var response = await client.GetAsync("/BackOffice/Slider/Create");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("id=\"__btnCreateSlider__\"", body);
    }

    // ---- GetSliderItemForm ----

    [Fact]
    public async Task ItemForm_CreateMode_WithSaveItemKey_ShowsCreateButton()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Slider.AccessItems, AccessKeys.Slider.SaveItem));
        var sliderId = await SeedSliderAsync(applicationId);

        var response = await client.GetAsync($"/BackOffice/Slider/GetSliderItemForm/{sliderId}/0");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"__btnCreateSliderItem__\"", body);
        Assert.DoesNotContain("id=\"__btnUpdateSliderItem__\"", body);
    }

    [Fact]
    public async Task ItemForm_EditMode_WithUpdateItemKey_ShowsUpdateAndNewButtons()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Slider.AccessItems, AccessKeys.Slider.UpdateItem));
        var sliderId = await SeedSliderAsync(applicationId);
        var itemId = await SeedSliderItemAsync(sliderId);

        var response = await client.GetAsync($"/BackOffice/Slider/GetSliderItemForm/{sliderId}/{itemId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"__btnUpdateSliderItem__\"", body);
        Assert.Contains("id=\"__btnNewSliderItem__\"", body);
        Assert.DoesNotContain("id=\"__btnCreateSliderItem__\"", body);
    }

    // ---- GetSliderItemList ----

    [Fact]
    public async Task ItemList_WithAllRowActionKeys_ShowsAllControls()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',',
            AccessKeys.Slider.AccessItems, AccessKeys.Slider.Activity, AccessKeys.Slider.DeleteItem, AccessKeys.Slider.UpdateItem));
        var sliderId = await SeedSliderAsync(applicationId);
        var itemId = await SeedSliderItemAsync(sliderId);

        var response = await client.GetAsync($"/BackOffice/Slider/GetSliderItemList/{sliderId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"activeSliderItem({itemId})", body); // freshly seeded item defaults to IsActive = false
        Assert.Contains($"deleteSliderItem({itemId})", body);
        Assert.Contains($"editSliderItem({itemId}, {sliderId})", body);
    }

    [Fact]
    public async Task ItemList_WithoutRowActionKeys_HidesAllControls()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Slider.AccessItems);
        var sliderId = await SeedSliderAsync(applicationId);
        var itemId = await SeedSliderItemAsync(sliderId);

        var response = await client.GetAsync($"/BackOffice/Slider/GetSliderItemList/{sliderId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain($"deleteSliderItem({itemId})", body);
        Assert.DoesNotContain($"editSliderItem({itemId}, {sliderId})", body);
    }

    // ---- Create (Slider) POST flow ----

    [Fact]
    public async Task SliderCreate_Post_WithSaveKey_CreatesSliderAndReturnsOk()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Slider.Save);
        var countBefore = await SliderCountAsync(applicationId);

        var response = await client.PostAsync("/BackOffice/Slider/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ApplicationId"] = applicationId.ToString(),
            ["Title"] = "New slider",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(countBefore + 1, await SliderCountAsync(applicationId));
    }

    [Fact]
    public async Task SliderCreate_Post_WithoutSaveKey_IsForbiddenAndDoesNotCreate()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Empty);
        var countBefore = await SliderCountAsync(applicationId);

        var response = await client.PostAsync("/BackOffice/Slider/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ApplicationId"] = applicationId.ToString(),
            ["Title"] = "New slider",
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(countBefore, await SliderCountAsync(applicationId));
    }

    [Fact]
    public async Task SliderCreate_Post_WithoutAntiforgeryToken_IsRejected()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Slider.Save);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        var response = await client.PostAsync("/BackOffice/Slider/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ApplicationId"] = applicationId.ToString(),
            ["Title"] = "New slider",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- CreateItem POST flow ----

    [Fact]
    public async Task SliderCreateItem_Post_WithSaveItemKey_CreatesItemAndReturnsOk()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Slider.SaveItem);
        var sliderId = await SeedSliderAsync(applicationId);
        var countBefore = await SliderItemCountAsync(sliderId);

        var response = await client.PostAsync("/BackOffice/Slider/CreateItem", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SliderId"] = sliderId.ToString(),
            ["Title"] = "New item",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(countBefore + 1, await SliderItemCountAsync(sliderId));
    }

    [Fact]
    public async Task SliderCreateItem_Post_WithoutSaveItemKey_IsForbiddenAndDoesNotCreate()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Empty);
        var sliderId = await SeedSliderAsync(applicationId);
        var countBefore = await SliderItemCountAsync(sliderId);

        var response = await client.PostAsync("/BackOffice/Slider/CreateItem", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SliderId"] = sliderId.ToString(),
            ["Title"] = "New item",
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(countBefore, await SliderItemCountAsync(sliderId));
    }

    // ---- UpdateItem POST flow ----

    [Fact]
    public async Task SliderUpdateItem_Post_WithUpdateItemKey_UpdatesAndReturnsOk()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Slider.UpdateItem);
        var sliderId = await SeedSliderAsync(applicationId);
        var itemId = await SeedSliderItemAsync(sliderId);

        var response = await client.PostAsync("/BackOffice/Slider/UpdateItem", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = itemId.ToString(),
            ["SliderId"] = sliderId.ToString(),
            ["Title"] = "Changed title",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Changed title", await SliderItemTitleAsync(itemId));
    }

    [Fact]
    public async Task SliderUpdateItem_Post_WithoutUpdateItemKey_IsForbiddenAndDoesNotMutate()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Empty);
        var sliderId = await SeedSliderAsync(applicationId);
        var itemId = await SeedSliderItemAsync(sliderId);

        var response = await client.PostAsync("/BackOffice/Slider/UpdateItem", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = itemId.ToString(),
            ["SliderId"] = sliderId.ToString(),
            ["Title"] = "Changed title",
        }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Item", await SliderItemTitleAsync(itemId));
    }
}
