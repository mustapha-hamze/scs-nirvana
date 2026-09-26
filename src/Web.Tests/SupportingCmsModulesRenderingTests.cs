using System.Net;
using System.Net.Http;
using Domains.Entities.ContentManagement;
using Domains.Entities.CustomModule;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// Rendering coverage for the supporting CMS modules (categories, schemas, sliders, tags, cultures,
// system types, application settings): list partials show an empty state instead of an empty
// table, rows use the shared design-system classes, forms keep labelled fields plus the save hooks
// their scripts call, and the slider/schema tools render bounded media and labelled actions.
public sealed class SupportingCmsModulesRenderingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SupportingCmsModulesRenderingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, int ApplicationId)> SuperAdminAsync()
    {
        var email = $"cms-modules-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        return (client, applicationId);
    }

    private async Task<T> SeedAsync<T>(T entity) where T : class
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    private static async Task<string> GetOkAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    public static TheoryData<string, string> EmptyLists => new()
    {
        { "/BackOffice/Category/List", "No categories yet." },
        { "/BackOffice/Schema/SchemaList", "No schemas yet." },
        { "/BackOffice/Slider/List", "No sliders yet." },
    };

    [Theory]
    [MemberData(nameof(EmptyLists))]
    public async Task EmptyList_RendersEmptyStateInsteadOfTable(string url, string message)
    {
        var (client, _) = await SuperAdminAsync();

        var body = await GetOkAsync(client, url);

        Assert.Contains("scs-empty-state", body);
        Assert.Contains(message, body);
        Assert.DoesNotContain("<table", body);
    }

    [Fact]
    public async Task CategoryList_ShowsParentAndSortableDate_WithoutDeadDeleteControl()
    {
        var (client, applicationId) = await SuperAdminAsync();
        var parent = await SeedAsync(new Category { ApplicationId = applicationId, Title = "Parent cat" });
        await SeedAsync(new Category { ApplicationId = applicationId, Title = "Child cat", ParentId = parent.Id });

        var body = await GetOkAsync(client, "/BackOffice/Category/List");

        Assert.Contains("id=\"datatable-buttons\"", body);
        Assert.Contains("Top level", body);
        Assert.Contains("data-order=\"", body);
        Assert.DoesNotContain("Delete", body); // no delete endpoint exists, so no control is offered
    }

    [Fact]
    public async Task SchemaList_RowActions_AreLabelledButtonsKeepingHooks()
    {
        var (client, applicationId) = await SuperAdminAsync();
        var schema = await SeedAsync(new Schema { ApplicationId = applicationId, Title = "Hero", LogoFileName = "logo.png", TypeId = 1000 });

        var body = await GetOkAsync(client, "/BackOffice/Schema/SchemaList");

        Assert.Contains("class=\"scs-logo-thumb\"", body);
        Assert.Contains($"onclick=\"newSchemaDetailsForm({schema.Id})\"", body);
        Assert.Contains($"onclick=\"schemaForm({schema.Id})\"", body);
        Assert.Matches($@"<button type=""button"" class=""action-icon is-danger"" onclick=""deleteSchema\({schema.Id}\)""", body);
        Assert.Contains("aria-label=\"Delete Hero\"", body);
    }

    [Fact]
    public async Task SchemaDetailsList_Empty_RendersEmptyState()
    {
        var (client, applicationId) = await SuperAdminAsync();
        var schema = await SeedAsync(new Schema { ApplicationId = applicationId, Title = "Empty", LogoFileName = "logo.png", TypeId = 1000 });

        var body = await GetOkAsync(client, $"/BackOffice/Schema/SchemaDetailsList/{schema.Id}");

        Assert.Contains("This schema has no fields yet.", body);
    }

    [Fact]
    public async Task SchemaForm_EditMode_ShowsBoundedLogoPreviewWithReplaceAction()
    {
        var (client, applicationId) = await SuperAdminAsync();
        var schema = await SeedAsync(new Schema { ApplicationId = applicationId, Title = "Card", LogoFileName = "card.png", TypeId = 1000 });

        var body = await GetOkAsync(client, $"/BackOffice/Schema/SchemaForm/{schema.Id}");

        Assert.Contains("id=\"schemaLogoPreviewPlaceHolder\"", body);
        Assert.Contains("scs-logo-preview", body);
        Assert.Contains("onclick=\"removeLogo()\"", body);
        Assert.Contains("id=\"file-upload-SchemaLogo\"", body);
    }

    // (form url, save hook the feature script calls, a field-help or validation marker).
    public static TheoryData<string, string, string> Forms => new()
    {
        { "/BackOffice/Category/Form", "onclick=\"saveCategoryForm()\"", "aria-describedby=\"categoryTitleHelp\"" },
        { "/BackOffice/Schema/SchemaForm/0", "onclick=\"saveSchemaForm()\"", "aria-describedby=\"schemaLogoHelp\"" },
        { "/BackOffice/General/TagForm", "onclick=\"saveTagForm()\"", "class=\"form-label scs-required\"" },
        { "/BackOffice/General/CultureForm", "onclick=\"saveCultureForm()\"", "aria-describedby=\"cultureKeyHelp\"" },
        { "/BackOffice/General/SystemTypeForm", "onclick=\"saveSystemTypeForm()\"", "class=\"form-check-input\"" },
        { "/BackOffice/General/ApplicationSettingForm", "onclick=\"saveApplicationSettingForm()\"", "aria-describedby=\"applicationSettingValueHelp\"" },
    };

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task ModalForms_KeepSaveHook_AddCancelAndFieldHelp(string url, string saveHook, string helpMarker)
    {
        var (client, _) = await SuperAdminAsync();

        var body = await GetOkAsync(client, url);

        Assert.Contains(saveHook, body);
        Assert.Contains("data-bs-dismiss=\"modal\">Cancel</button>", body);
        Assert.Contains(helpMarker, body);
        Assert.Contains("invalid-feedback", body);
    }

    [Fact]
    public async Task SchemaDetailsForm_WidthIsBoundedNumber()
    {
        var (client, applicationId) = await SuperAdminAsync();
        var schema = await SeedAsync(new Schema { ApplicationId = applicationId, Title = "Fields", LogoFileName = "logo.png", TypeId = 1000 });

        var body = await GetOkAsync(client, $"/BackOffice/Schema/SchemaDetailsForm/{schema.Id}");

        Assert.Matches(@"type=""number""[^>]*min=""1"" max=""12""", body);
        Assert.Contains("id=\"btnSaveSchemaDetailsForm\"", body);
    }

    [Fact]
    public async Task SliderItemList_RendersBoundedMediaCardsWithStatusAndLabelledActions()
    {
        var (client, applicationId) = await SuperAdminAsync();
        var slider = await SeedAsync(new Slider { ApplicationId = applicationId, Title = "Home" });
        var item = await SeedAsync(new SliderItem { SliderId = slider.Id, Title = "Spring", ImageFileName = "spring.jpg" });

        var body = await GetOkAsync(client, $"/BackOffice/Slider/GetSliderItemList/{slider.Id}");

        Assert.Contains("scs-media-card scs-media-card--wide", body);
        Assert.Contains("<span class=\"scs-status\">Inactive</span>", body);
        Assert.Contains($"onclick=\"activeSliderItem({item.Id})\"", body);
        Assert.Contains("aria-label=\"Delete Spring\"", body);
        Assert.Contains("aria-label=\"Edit Spring\"", body);
    }

    [Fact]
    public async Task SliderItemList_Empty_RendersEmptyState()
    {
        var (client, applicationId) = await SuperAdminAsync();
        var slider = await SeedAsync(new Slider { ApplicationId = applicationId, Title = "Empty" });

        var body = await GetOkAsync(client, $"/BackOffice/Slider/GetSliderItemList/{slider.Id}");

        Assert.Contains("No slides yet.", body);
    }

    [Fact]
    public async Task SliderItemForm_CreateMode_HasPreviewTargetForChosenImage()
    {
        var (client, applicationId) = await SuperAdminAsync();
        var slider = await SeedAsync(new Slider { ApplicationId = applicationId, Title = "Preview" });

        var body = await GetOkAsync(client, $"/BackOffice/Slider/GetSliderItemForm/{slider.Id}/0");

        Assert.Contains("onchange=\"previewSliderItemImage(this)\"", body);
        Assert.Contains("id=\"sliderItemImagePreview\"", body);
        Assert.Contains("id=\"sliderItemImageEmpty\"", body);
    }
}
