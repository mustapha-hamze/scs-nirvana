using System.Net;
using System.Net.Http;
using System.Threading;
using Application.UseCases.UserManagementServices;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Web.Authorization;
using Xunit;

namespace Web.Tests;

// Web Phase 4 tasks 3 and its fix pass: rendering/binding regression coverage for the Content
// feature's strongly typed view models (Web/Areas/BackOffice/Features/Content/ViewModels). Proves
// the create/edit ContentForm tab and button visibility, ContentList's per-row action controls,
// Farsi, sections, images, and relations/metadata forms still gate on the same access keys the
// old ViewData/accesses.Contains checks used, and that every Can* flag in a request - however
// many - is resolved from the single request-scoped access snapshot
// (BackOfficeShellContext/AccessKeyAuthorizer's shared per-request cache), never a second
// GetUserAccesses call.
public sealed class ContentFeatureViewModelRenderingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContentFeatureViewModelRenderingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SeedContentAsync(int applicationId, string categories = "", string tags = "", string cultures = "")
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var content = new Content
        {
            ApplicationId = applicationId,
            TypeId = 1000,
            Title = "Rendering content",
            PublishDt = DateTime.UtcNow,
            Categories = categories,
            Tags = tags,
            Cultures = cultures,
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        return content.Id;
    }

    private async Task SeedApplicationSettingAsync(int applicationId, int settingId, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.ApplicationSettings.Add(new ApplicationSetting { ApplicationId = applicationId, SettingId = settingId, Title = "Setting", Value = value });
        await context.SaveChangesAsync();
    }

    private async Task<(HttpClient Client, Infrastructure.Identity.ApplicationUser User, int ApplicationId)> SeedTenantMemberAsync(string keys)
    {
        var email = $"content-render-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        if (!string.IsNullOrEmpty(keys))
            await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, keys);
        return (client, user, applicationId);
    }

    // ---- ContentForm ----

    [Fact]
    public async Task ContentForm_Create_WithAddKey_RendersSaveButtonAndNoTabs()
    {
        var (client, _, _) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Content.Add, AccessKeys.Content.Save));

        var response = await client.GetAsync("/BackOffice/Content/ContentForm/0/1000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"btnSaveContentForm\"", body);
        Assert.DoesNotContain("href=\"#body\"", body);
        Assert.DoesNotContain("href=\"#images\"", body);
        Assert.DoesNotContain("href=\"#relations\"", body);
        Assert.DoesNotContain("href=\"#metadata\"", body);
        Assert.DoesNotContain("href=\"#attachment\"", body);
    }

    [Fact]
    public async Task ContentForm_Edit_WithGrantedPreviewKeys_RendersOnlyThoseTabs()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',',
            AccessKeys.Content.Edit, AccessKeys.Content.Update, AccessKeys.Content.ChangeActivity,
            AccessKeys.Content.PreviewBody, AccessKeys.Content.PreviewRelations));
        var contentId = await SeedContentAsync(applicationId);
        await SeedApplicationSettingAsync(applicationId, 5000, "https://example.test");

        var response = await client.GetAsync($"/BackOffice/Content/ContentForm/{contentId}/1000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"#body\"", body);
        Assert.Contains("href=\"#relations\"", body);
        Assert.DoesNotContain("href=\"#images\"", body);
        Assert.DoesNotContain("href=\"#metadata\"", body);
        Assert.DoesNotContain("href=\"#attachment\"", body);
        Assert.Contains("id=\"btnSaveContentForm\"", body);
        Assert.Contains($"changeContentActiveMode(1000, {contentId}", body);
        Assert.Contains($"href=\"https://example.test/{contentId}\"", body);
    }

    [Fact]
    public async Task ContentForm_Edit_WithoutAnyPreviewKeys_HidesAllTabs()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Content.Edit);
        var contentId = await SeedContentAsync(applicationId);
        await SeedApplicationSettingAsync(applicationId, 5000, "https://example.test");

        var response = await client.GetAsync($"/BackOffice/Content/ContentForm/{contentId}/1000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var tab in new[] { "#body", "#images", "#relations", "#metadata", "#attachment" })
            Assert.DoesNotContain($"href=\"{tab}\"", body);
        Assert.DoesNotContain("id=\"btnSaveContentForm\"", body);
    }

    // ---- ContentList action controls ----

    [Fact]
    public async Task ContentList_WithEditKeyOnly_RendersEditNotDelete()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Content.Module, AccessKeys.Content.Edit));
        var contentId = await SeedContentAsync(applicationId);

        var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/BackOffice/Content/ContentForm/{contentId}/1000", body);
        Assert.DoesNotContain($"deleteContent({contentId})", body);
        // The Farsi link has no access-key gate, preserved unconditionally.
        Assert.Contains($"/BackOffice/Content/FarsiContentForm/{contentId}/1000", body);
    }

    private WebApplicationFactory<Program> BuildCounterFactory(out CallCounter counter)
    {
        var counterFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<CallCounter>();
                services.RemoveAll<IUserManagementServices>();
                services.AddTransient<IUserManagementServices>(sp =>
                    new CountingUserManagementServicesDecorator(
                        ActivatorUtilities.CreateInstance<UserManagementServices>(sp),
                        sp.GetRequiredService<CallCounter>()));
            });
        });
        counter = counterFactory.Services.GetRequiredService<CallCounter>();
        return counterFactory;
    }

    // Web Phase 4 fix: proves GetUserAccesses is called exactly once for the whole request -
    // RequireAccessAttribute's own check plus ContentList's CanEdit/CanDelete flags share
    // AccessKeyAuthorizer's per-request cache - not merely that the count stays constant across
    // row counts (row-count independence is a corollary of "exactly once", checked here too).
    [Fact]
    public async Task ContentList_ResolvesAccessesExactlyOncePerRequest_RegardlessOfRowCount()
    {
        var counterFactory = BuildCounterFactory(out var counter);

        var email = $"content-list-dupcheck-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(counterFactory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(counterFactory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(counterFactory, client, user);
        await AccountFlowHelper.GrantAccessAsync(counterFactory, user, applicationId,
            string.Join(',', AccessKeys.Content.Module, AccessKeys.Content.Edit, AccessKeys.Content.Delete));

        async Task<int> AddRowsAndGetCallCountAsync(int rowsToAdd)
        {
            using (var scope = counterFactory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                for (var i = 0; i < rowsToAdd; i++)
                    context.Contents.Add(new Content { ApplicationId = applicationId, TypeId = 1000, Title = $"Row {i}", PublishDt = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }
            counter.Reset();
            var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return counter.GetUserAccessesCalls;
        }

        Assert.Equal(1, await AddRowsAndGetCallCountAsync(1));
        Assert.Equal(1, await AddRowsAndGetCallCountAsync(3)); // 1 + 3 = 4 rows total now
    }

    // Web Phase 4 fix: a fully rendered ContentForm resolves seven distinct Can* flags
    // (CanSaveOrUpdateContent, CanChangeActivity, and five CanPreview* tab flags) plus
    // DenyIfMissingAccessAsync's own dynamic check - all from the same request-scoped snapshot,
    // so GetUserAccesses is still called exactly once for a non-SuperAdmin holding every one of
    // those keys (the case with the most visible controls, and so the most checks).
    [Fact]
    public async Task ContentForm_FullyRendered_ResolvesAccessesExactlyOncePerRequest()
    {
        var counterFactory = BuildCounterFactory(out var counter);

        var email = $"content-form-dupcheck-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(counterFactory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(counterFactory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(counterFactory, client, user);
        await AccountFlowHelper.GrantAccessAsync(counterFactory, user, applicationId, string.Join(',',
            AccessKeys.Content.Edit, AccessKeys.Content.Update, AccessKeys.Content.ChangeActivity,
            AccessKeys.Content.PreviewBody, AccessKeys.Content.PreviewImages, AccessKeys.Content.PreviewAttachments,
            AccessKeys.Content.PreviewRelations, AccessKeys.Content.PreviewMetadata));
        int contentId;
        using (var scope = counterFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var content = new Content { ApplicationId = applicationId, TypeId = 1000, Title = "Dup-check content", PublishDt = DateTime.UtcNow };
            context.Contents.Add(content);
            await context.SaveChangesAsync();
            context.ApplicationSettings.Add(new ApplicationSetting { ApplicationId = applicationId, SettingId = 5000, Title = "WebsiteUrl", Value = "https://example.test" });
            await context.SaveChangesAsync();
            contentId = content.Id;
        }
        counter.Reset();

        var response = await client.GetAsync($"/BackOffice/Content/ContentForm/{contentId}/1000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Confirm this really is the "multiple visible controls" case, not an early-out.
        Assert.Contains("id=\"btnSaveContentForm\"", body);
        Assert.Contains("href=\"#body\"", body);
        Assert.Contains("href=\"#images\"", body);
        Assert.Contains("href=\"#attachment\"", body);
        Assert.Contains("href=\"#relations\"", body);
        Assert.Contains("href=\"#metadata\"", body);
        Assert.Equal(1, counter.GetUserAccessesCalls);
    }

    // ---- Farsi ----

    [Fact]
    public async Task FarsiContentForm_NoFarsiContentYet_ShowsFallbackNoticeAndRouteTypeId()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Content.EditFarsi);
        var contentId = await SeedContentAsync(applicationId);

        var response = await client.GetAsync($"/BackOffice/Content/FarsiContentForm/{contentId}/1000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("alert-info", body);
        Assert.Contains("pre-filled from the English", body);
        Assert.Contains("id=\"hidTypeIdContent_Form\" value=\"1000\"", body);
    }

    // ---- Sections ----

    [Fact]
    public async Task ContentSections_WithSaveBodyKey_RendersPriorityAndSaveControls()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Content.PreviewBody, AccessKeys.Content.SaveBody));
        var contentId = await SeedContentAsync(applicationId);

        var response = await client.GetAsync($"/BackOffice/Content/ContentSections/{contentId}/1000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"hidPriority__ContentSection\" value=\"0\"", body);
        Assert.Contains("id=\"btnSaveContentBody\"", body);
        Assert.Contains("id=\"btnUpdateContentBodyLayout\"", body);
    }

    [Fact]
    public async Task ContentSections_WithoutSaveBodyKey_HidesSaveControls()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Content.PreviewBody);
        var contentId = await SeedContentAsync(applicationId);

        var response = await client.GetAsync($"/BackOffice/Content/ContentSections/{contentId}/1000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("id=\"btnSaveContentBody\"", body);
    }

    // ---- Images ----

    [Fact]
    public async Task ContentImages_WithUploadImagesKey_RendersUploadControls()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Content.PreviewImages, AccessKeys.Content.UploadImages));
        var contentId = await SeedContentAsync(applicationId);
        await SeedApplicationSettingAsync(applicationId, 1001, "16:9");
        await SeedApplicationSettingAsync(applicationId, 1000, "100-100");

        var response = await client.GetAsync($"/BackOffice/Content/ContentImages/{contentId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"ddlImageSetting\"", body);
        Assert.Contains("16:9", body);
    }

    [Fact]
    public async Task ContentImages_WithoutUploadImagesKey_HidesUploadControls()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Content.PreviewImages);
        var contentId = await SeedContentAsync(applicationId);
        await SeedApplicationSettingAsync(applicationId, 1001, "16:9");
        await SeedApplicationSettingAsync(applicationId, 1000, "100-100");

        var response = await client.GetAsync($"/BackOffice/Content/ContentImages/{contentId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("id=\"ddlImageSetting\"", body);
    }

    // ---- Relations / Metadata ----

    [Fact]
    public async Task ContentRelations_RendersRelatedHiddenValuesAndGatesSaveButton()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(AccessKeys.Content.PreviewRelations);
        var contentId = await SeedContentAsync(applicationId, categories: "1|2|", tags: "3|", cultures: "1|");

        var response = await client.GetAsync($"/BackOffice/Content/ContentRelations/{contentId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"hidCategoriesRelated\" value=\"1|2|\"", body);
        Assert.Contains("id=\"hidTagsRelated\" value=\"3|\"", body);
        Assert.Contains("id=\"hidCulturesRelated\" value=\"1|\"", body);
        Assert.DoesNotContain("id=\"btnSaveContentRelations\"", body);
    }

    [Fact]
    public async Task ContentMetadata_WithSaveMetadataKey_BindsContentIdAndShowsSaveButton()
    {
        var (client, _, applicationId) = await SeedTenantMemberAsync(string.Join(',', AccessKeys.Content.PreviewMetadata, AccessKeys.Content.SaveMetadata));
        var contentId = await SeedContentAsync(applicationId);

        var response = await client.GetAsync($"/BackOffice/Content/ContentMetadata/{contentId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"id=\"ContentId\" name=\"ContentId\" value=\"{contentId}\"", body);
        Assert.Contains("id=\"btnSaveContentMetadata\"", body);
    }

    private sealed class CallCounter
    {
        public int GetUserAccessesCalls;
        public void Reset() => GetUserAccessesCalls = 0;
    }

    private sealed class CountingUserManagementServicesDecorator : IUserManagementServices
    {
        private readonly IUserManagementServices _inner;
        private readonly CallCounter _counter;

        public CountingUserManagementServicesDecorator(IUserManagementServices inner, CallCounter counter)
        {
            _inner = inner;
            _counter = counter;
        }

        public Task<List<Application.Contracts.UserManagement.UserDto>> List(bool isAdminUser, string email = "", CancellationToken cancellationToken = default) =>
            _inner.List(isAdminUser, email, cancellationToken);

        public Task<Application.Contracts.UserManagement.UserDto> GetUserByEmailAddress(string email, CancellationToken cancellationToken = default) =>
            _inner.GetUserByEmailAddress(email, cancellationToken);

        public Task<string> GetUserAccesses(string email, int appId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _counter.GetUserAccessesCalls);
            return _inner.GetUserAccesses(email, appId, cancellationToken);
        }

        public Task SetCurrentApplicationId(string email, int appId, CancellationToken cancellationToken = default) =>
            _inner.SetCurrentApplicationId(email, appId, cancellationToken);

        public Task SetUserAccesses(string accesses, string userId, int appId, CancellationToken cancellationToken = default) =>
            _inner.SetUserAccesses(accesses, userId, appId, cancellationToken);
    }
}
