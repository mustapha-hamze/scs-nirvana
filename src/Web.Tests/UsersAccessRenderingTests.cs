using System.Net;
using System.Net.Http;
using Domains.Entities.AccessManagement;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// Rendering coverage for the Users and Access Management screens: labelled filters and forms keep
// the IDs, names and onclick hooks their scripts call, lists show an empty state instead of an
// empty table, row actions are labelled buttons, and controls with no handler behind them are gone.
// The per-user access modal (UserSettingForm, Sectors, Entities and their partials) is not covered
// here: those AccountController actions currently fail before rendering (see the commit notes).
public sealed class UsersAccessRenderingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UsersAccessRenderingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, int ApplicationId, string UserId)> SuperAdminAsync()
    {
        var email = $"users-access-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        return (client, applicationId, user.Id);
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

    [Fact]
    public async Task UsersPage_FilterIsLabelled_AndApprovalFilterIsNeverPosted()
    {
        var (client, _, _) = await SuperAdminAsync();

        var body = await GetOkAsync(client, "/BackOffice/Account/Users");

        Assert.Contains("id=\"frmUserFilters\"", body);
        Assert.Contains("onsubmit=\"findUsers(); return false;\"", body);
        Assert.Contains("name=\"IsAdminUser\"", body);
        Assert.Contains("name=\"Email\"", body);
        Assert.Matches("<select class=\"form-select\" id=\"userApprovalFilter\" onchange=\"filterUsersByApproval\\(\\)\">", body);
        Assert.Contains("id=\"btnUserFilter\"", body);
        Assert.Contains("class=\"scs-table-wrap\" id=\"findUsersResultBody\"", body);
        Assert.Contains("onclick=\"newUserForm()\"", body);
        Assert.Contains("id=\"userSettingStatus\"", body);
    }

    [Fact]
    public async Task UserForm_Create_ShowsPasswordFields_AndLabelledActions()
    {
        var (client, _, _) = await SuperAdminAsync();

        var body = await GetOkAsync(client, "/BackOffice/Account/UserForm");

        Assert.Contains("id=\"frmUser\"", body);
        Assert.Contains("data-mode=\"create\"", body);
        Assert.Contains("name=\"Password\"", body);
        Assert.Contains("name=\"ConfirmPassword\"", body);
        Assert.Contains("id=\"birthdate\"", body);
        Assert.Contains("<label class=\"form-label\" for=\"HomeAddress\">Home address</label>", body);
        Assert.Contains("onclick=\"saveUserForm()\"", body);
        Assert.Contains("Create user", body);
        Assert.Contains("data-bs-dismiss=\"modal\">Cancel</button>", body);
    }

    [Fact]
    public async Task UserForm_Edit_LocksEmail_AndRecordsInitialAccessFlags()
    {
        var (client, _, userId) = await SuperAdminAsync();

        var body = await GetOkAsync(client, $"/BackOffice/Account/UserForm/{userId}");

        Assert.Contains("data-mode=\"edit\"", body);
        Assert.DoesNotContain("name=\"Password\"", body);
        Assert.Matches("<input(?=[^>]*name=\"EmailAddress\")(?=[^>]*readonly)", body);
        // The seeded super admin is approved and an admin panel user.
        Assert.Matches("<input(?=[^>]*id=\"IsApprove\")(?=[^>]*data-initial=\"true\")", body);
        Assert.Matches("<input(?=[^>]*id=\"IsAdminUser\")(?=[^>]*data-initial=\"true\")", body);
        Assert.Contains("Save user", body);
    }

    [Fact]
    public async Task RolesPage_ListsRoles_WithLabelledCreateForm()
    {
        var (client, _, _) = await SuperAdminAsync();

        var body = await GetOkAsync(client, "/BackOffice/Account/Roles");

        Assert.Contains("<label class=\"form-label scs-required\" for=\"txtRoleName\">Role name</label>", body);
        Assert.Contains("name=\"RoleName\"", body);
        Assert.Contains("title=\"SuperAdmin\">SuperAdmin</td>", body);
        Assert.DoesNotContain("No roles yet.", body);
    }

    [Fact]
    public async Task SectorList_Empty_RendersEmptyState()
    {
        var (client, _, _) = await SuperAdminAsync();

        var body = await GetOkAsync(client, "/BackOffice/AccessManagement/SectorList");

        Assert.Contains("No sectors yet.", body);
        Assert.DoesNotContain("<table", body);
    }

    [Fact]
    public async Task SectorList_RowAction_IsLabelledEntitiesButton_WithoutDeadEdit()
    {
        var (client, applicationId, _) = await SuperAdminAsync();
        var sector = await SeedAsync(new Sector { ApplicationId = applicationId, Title = "Content", IsActive = true });

        var body = await GetOkAsync(client, "/BackOffice/AccessManagement/SectorList");

        Assert.Contains("id=\"datatable-buttons\"", body);
        Assert.Contains($"data-bs-target=\"#sectorEntity-modal\" onclick=\"entities({sector.Id})\"", body);
        Assert.Contains("aria-label=\"Manage entities in Content\"", body);
        Assert.DoesNotContain("title=\"Edit\"", body); // no edit handler exists, so no control is offered
    }

    [Fact]
    public async Task SectorForm_KeepsSaveHook_WithLabelledTitle()
    {
        var (client, _, _) = await SuperAdminAsync();

        var body = await GetOkAsync(client, "/BackOffice/AccessManagement/SectorForm");

        Assert.Contains("id=\"frmSaveSector\"", body);
        Assert.Contains("<label class=\"form-label scs-required\" for=\"sectorTitle\">Sector title</label>", body);
        Assert.Contains("name=\"Title\"", body);
        Assert.Contains("onclick=\"saveSector()\"", body);
    }

    [Fact]
    public async Task EntityFormAndList_ShowSectorContext_AndEmptyState()
    {
        var (client, applicationId, _) = await SuperAdminAsync();
        var sector = await SeedAsync(new Sector { ApplicationId = applicationId, Title = "Media", IsActive = true });

        var form = await GetOkAsync(client, $"/BackOffice/AccessManagement/EntityForm/{sector.Id}");
        var list = await GetOkAsync(client, $"/BackOffice/AccessManagement/EntityList/{sector.Id}");

        Assert.Contains("<strong class=\"text-body\">Media</strong>", form);
        Assert.Contains("id=\"frmSaveSectorEntity\"", form);
        Assert.Matches($"<input(?=[^>]*name=\"SectorId\")(?=[^>]*value=\"{sector.Id}\")", form);
        Assert.Contains("name=\"AccessKey\"", form);
        Assert.Contains("onclick=\"saveEntity()\"", form);
        Assert.Contains("No entities yet.", list);
    }

    [Fact]
    public async Task AccessList_Empty_RendersEmptyState()
    {
        var (client, _, _) = await SuperAdminAsync();

        var body = await GetOkAsync(client, "/BackOffice/AccessManagement/AccessList");

        Assert.Contains("No accesses yet.", body);
        Assert.DoesNotContain("<table", body);
    }

    [Fact]
    public async Task AccessFormAndList_EditMode_WarnsAboutRename_AndRowEditIsLabelled()
    {
        var (client, applicationId, _) = await SuperAdminAsync();
        var sector = await SeedAsync(new Sector { ApplicationId = applicationId, Title = "Content", IsActive = true });
        var entity = await SeedAsync(new SectorEntity { SectorId = sector.Id, Title = "Pages", AccessKey = "CMS_Pages", IsActive = true });
        var access = await SeedAsync(new EntityAccess { EntityId = entity.Id, Access = "CMS_Pages_Edit", IsActive = true });

        var form = await GetOkAsync(client, $"/BackOffice/AccessManagement/AccessForm?id={access.Id}");
        var list = await GetOkAsync(client, "/BackOffice/AccessManagement/AccessList");

        Assert.Contains("id=\"frmSaveAccess\"", form);
        Assert.Contains("data-original=\"CMS_Pages_Edit\"", form);
        Assert.Contains("nobody has the new key until you grant it", form);
        Assert.Contains("id=\"sectorEntitiesDDL\"", form);
        Assert.Contains("onclick=\"saveAccess()\"", form);

        Assert.Contains($"onclick=\"accessEditForm({access.Id})\"", list);
        Assert.Contains("aria-label=\"Edit access CMS_Pages_Edit\"", list);
        Assert.Contains("<code class=\"scs-code\">CMS_Pages_Edit</code>", list);
    }
}
