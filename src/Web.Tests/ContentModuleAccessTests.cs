using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Domains.Entities.AccessManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Keys = Web.Authorization.AccessKeys.Content;

namespace Web.Tests;

// An Editor holding every Content action key but not the base module key (CMS1000_1001) must not
// get Content navigation or the Content index - action keys never imply the module key. The module
// key is granted explicitly through the SuperAdmin permissions screen, which must offer it on the
// Content entity even when no EntityAccess row defines it. Dashboard and sidebar share one exact rule.
public sealed class ContentModuleAccessTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Password = "CorrectHorseBattery12";

    private static readonly string[] ActionKeys =
    [
        Keys.Add, Keys.Save, Keys.Update, Keys.Edit, Keys.Delete, Keys.ChangeActivity, Keys.EditFarsi,
        Keys.PreviewBody, Keys.SaveBody, Keys.PreviewImages, Keys.UploadImages, Keys.PreviewAttachments,
        Keys.PreviewRelations, Keys.SaveRelations, Keys.PreviewMetadata, Keys.SaveMetadata,
    ];

    private static readonly string AllActionKeys = string.Join(",", ActionKeys) + ",";

    private readonly TestWebApplicationFactory _factory;

    public ContentModuleAccessTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record Member(ApplicationUser User, HttpClient Client, int ApplicationId, int ContentTypeId);

    private async Task<Member> EditorAsync(string accesses, bool superAdmin = false)
    {
        var email = $"content-module-{Guid.NewGuid():N}@test.local";
        var user = superAdmin
            ? await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, Password)
            : await AccountFlowHelper.SeedAdminUserAsync(_factory, email, Password);
        if (!superAdmin)
        {
            using var scope = _factory.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (!await roles.RoleExistsAsync("Editor"))
                await roles.CreateAsync(new IdentityRole("Editor"));
            await users.AddToRoleAsync((await users.FindByIdAsync(user.Id))!, "Editor");
        }

        var client = await AccountFlowHelper.LoginAsync(_factory, email, Password);
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var typeId = await SeedContentTypeAsync(applicationId);
        if (accesses.Length > 0)
            await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, accesses);
        return new Member(user, client, applicationId, typeId);
    }

    private async Task<int> SeedContentTypeAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var type = new SystemType
        {
            ApplicationId = applicationId,
            TypeGroupId = Application.UseCases.Utilities.ApplicationConst.TypeId.Content,
            Title = "News",
            IsActive = true,
        };
        context.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    // The Content entity as existing databases define it: the 16 action keys, no module-key row.
    private async Task<int> SeedContentEntityAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sector = new Sector { ApplicationId = applicationId, Title = $"CMS {Guid.NewGuid():N}", IsActive = true };
        context.Add(sector);
        await context.SaveChangesAsync();
        var entity = new SectorEntity { SectorId = sector.Id, Title = "Content", AccessKey = Keys.Module, IsActive = true };
        context.Add(entity);
        await context.SaveChangesAsync();
        foreach (var key in ActionKeys)
            context.Add(new EntityAccess { EntityId = entity.Id, Access = key, IsActive = true });
        await context.SaveChangesAsync();
        return entity.Id;
    }

    private static async Task<string> DashboardAsync(HttpClient client)
    {
        var response = await client.GetAsync("/BackOffice/Home/Index");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static bool HasContentNav(string body) => body.Contains("id=\"liCMSContent\"") && body.Contains("id=\"cmsMenuLi\"");

    private static bool HasContentCard(string body) => body.Contains(">Content</h3>");

    private static void AssertNoBareContentIndexLink(string body) =>
        Assert.DoesNotMatch("href=\"/BackOffice/Content/Index/?\"", body);

    [Fact]
    public async Task Editor_WithEveryActionKeyButNoModuleKey_GetsNoContentNavigationAndIsForbidden()
    {
        var m = await EditorAsync(AllActionKeys);

        var body = await DashboardAsync(m.Client);
        Assert.False(HasContentNav(body));
        Assert.False(HasContentCard(body));
        Assert.DoesNotContain("/BackOffice/Content/Index", body);
        Assert.DoesNotContain(">Categories</h3>", body);
        Assert.DoesNotContain(">Schemas</h3>", body);
        Assert.Contains("No modules assigned yet", body);

        Assert.Equal(HttpStatusCode.Forbidden, (await m.Client.GetAsync($"/BackOffice/Content/Index/{m.ContentTypeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await m.Client.GetAsync($"/BackOffice/Content/ContentList/{m.ContentTypeId}")).StatusCode);
    }

    [Theory]
    [InlineData("CMS1000_10011")]
    [InlineData(Keys.Edit)]
    [InlineData("CMS1000_1001_EXTRA,cms1000_1001")]
    public async Task SimilarOrActionKey_NeverGrantsContentModule(string accesses)
    {
        var m = await EditorAsync(accesses);

        var body = await DashboardAsync(m.Client);
        Assert.False(HasContentNav(body));
        Assert.False(HasContentCard(body));
        Assert.Equal(HttpStatusCode.Forbidden, (await m.Client.GetAsync($"/BackOffice/Content/Index/{m.ContentTypeId}")).StatusCode);
    }

    [Fact]
    public async Task ModuleKeyGrantedInAnotherWorkspace_DoesNotGrantContentInTheSelectedOne()
    {
        var m = await EditorAsync(AllActionKeys + Keys.Module + ",");
        // Switch the session to a second workspace where only action keys are held.
        var otherApplicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, m.Client, m.User);
        var otherTypeId = await SeedContentTypeAsync(otherApplicationId);
        await AccountFlowHelper.GrantAccessAsync(_factory, m.User, otherApplicationId, AllActionKeys);

        var body = await DashboardAsync(m.Client);
        Assert.False(HasContentNav(body));
        Assert.False(HasContentCard(body));
        Assert.Equal(HttpStatusCode.Forbidden, (await m.Client.GetAsync($"/BackOffice/Content/Index/{otherTypeId}")).StatusCode);
    }

    [Fact]
    public async Task SuperAdminPermissionsScreen_OffersModuleKey_AndGrantingItOpensContentForTheEditor()
    {
        var editor = await EditorAsync(AllActionKeys);
        var admin = await EditorAsync("", superAdmin: true);
        var entityId = await SeedContentEntityAsync(editor.ApplicationId);

        var partial = await admin.Client.GetAsync($"/BackOffice/Account/EntityAccesses/{entityId}");
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var html = await partial.Content.ReadAsStringAsync();
        Assert.Single(Regex.Matches(html, $"id=\"{Keys.Module}\""));
        Assert.Contains($"onchange=\"setEntityAccessHideInput('{Keys.Module}')\"", html);
        Assert.Contains("Content module: browse content types and lists", html);
        Assert.All(ActionKeys, key => Assert.Contains($"id=\"{key}\"", html));
        // Listed ahead of the action keys.
        Assert.True(html.IndexOf($"id=\"{Keys.Module}\"", StringComparison.Ordinal) < html.IndexOf($"id=\"{ActionKeys[0]}\"", StringComparison.Ordinal));

        // What the permissions tab posts after ticking the module box (see account-access.test.mjs).
        var save = await admin.Client.PostAsync("/BackOffice/Account/SetAccessForUser", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = editor.User.Id,
            ["ApplicationId"] = editor.ApplicationId.ToString(),
            ["Accesses"] = AllActionKeys + Keys.Module + ",",
        }));
        Assert.Equal("Done", await save.Content.ReadAsStringAsync());

        var stored = await admin.Client.GetStringAsync($"/BackOffice/Account/GetUserAccess/{editor.User.Id}/{editor.ApplicationId}");
        var tokens = stored.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(tokens, t => t == Keys.Module);
        Assert.All(ActionKeys, key => Assert.Contains(key, tokens));

        var body = await DashboardAsync(editor.Client);
        Assert.True(HasContentNav(body));
        Assert.Contains("id=\"liCMSAppPages\"", body);
        Assert.Contains($"href=\"/BackOffice/Content/Index/{editor.ContentTypeId}\"", body);
        Assert.True(HasContentCard(body));
        Assert.Equal(HttpStatusCode.OK, (await editor.Client.GetAsync($"/BackOffice/Content/Index/{editor.ContentTypeId}")).StatusCode);
    }

    [Fact]
    public async Task PermissionsScreen_DoesNotOfferModuleKeyOnNonContentEntities()
    {
        var admin = await EditorAsync("", superAdmin: true);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sector = new Sector { ApplicationId = admin.ApplicationId, Title = $"SCM {Guid.NewGuid():N}", IsActive = true };
        context.Add(sector);
        await context.SaveChangesAsync();
        var entity = new SectorEntity { SectorId = sector.Id, Title = "Sliders", AccessKey = Web.Authorization.AccessKeys.Slider.Module, IsActive = true };
        context.Add(entity);
        await context.SaveChangesAsync();
        // Slider's Add gate reuses the Content Add key verbatim - that alone is not a Content entity.
        context.Add(new EntityAccess { EntityId = entity.Id, Access = Web.Authorization.AccessKeys.Slider.Add, IsActive = true });
        context.Add(new EntityAccess { EntityId = entity.Id, Access = Web.Authorization.AccessKeys.Slider.Save, IsActive = true });
        await context.SaveChangesAsync();

        var html = await admin.Client.GetStringAsync($"/BackOffice/Account/EntityAccesses/{entity.Id}");

        Assert.DoesNotContain($"id=\"{Keys.Module}\"", html);
    }

    [Fact]
    public async Task ModuleKeyOnly_SidebarAndDashboardBothShowContentWithTypedLink()
    {
        var m = await EditorAsync(Keys.Module);

        var body = await DashboardAsync(m.Client);
        Assert.True(HasContentNav(body));
        Assert.True(HasContentCard(body));
        Assert.Contains($"href=\"/BackOffice/Content/Index/{m.ContentTypeId}\" class=\"btn btn-primary", body);
        Assert.DoesNotContain(">Categories</h3>", body);
        Assert.DoesNotContain(">Schemas</h3>", body);
        Assert.DoesNotContain("id=\"liCMSCategory\"", body);
        AssertNoBareContentIndexLink(body);
        Assert.Equal(HttpStatusCode.OK, (await m.Client.GetAsync($"/BackOffice/Content/Index/{m.ContentTypeId}")).StatusCode);
    }

    [Fact]
    public async Task ModuleAndActionKeys_SidebarAndDashboardBothShowContent()
    {
        var m = await EditorAsync(AllActionKeys + Keys.Module + ",");

        var body = await DashboardAsync(m.Client);
        Assert.True(HasContentNav(body));
        Assert.True(HasContentCard(body));
        AssertNoBareContentIndexLink(body);
    }

    [Fact]
    public async Task ModuleKeyButNoContentTypes_DashboardRendersNoContentCard()
    {
        var email = $"content-module-notypes-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, Password);
        var client = await AccountFlowHelper.LoginAsync(_factory, email, Password);
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Keys.Module);

        var body = await DashboardAsync(client);
        Assert.False(HasContentCard(body));
        Assert.DoesNotContain("/BackOffice/Content/Index", body[body.IndexOf("<main", StringComparison.Ordinal)..]);
    }

    [Fact]
    public async Task SuperAdmin_SidebarAndDashboardBothShowEveryCmsModule()
    {
        var m = await EditorAsync("", superAdmin: true);

        var body = await DashboardAsync(m.Client);
        Assert.True(HasContentNav(body));
        Assert.True(HasContentCard(body));
        Assert.Contains(">Categories</h3>", body);
        Assert.Contains(">Schemas</h3>", body);
        Assert.Contains(">Sliders</h3>", body);
        Assert.Contains($"href=\"/BackOffice/Content/Index/{m.ContentTypeId}\" class=\"btn btn-primary", body);
        AssertNoBareContentIndexLink(body);
    }

    [Fact]
    public async Task BareContentIndexRoute_IsNotMatched()
    {
        var m = await EditorAsync(Keys.Module);

        Assert.Equal(HttpStatusCode.NotFound, (await m.Client.GetAsync("/BackOffice/Content/Index")).StatusCode);
    }
}
