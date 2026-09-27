using System.Net;
using System.Net.Http;
using Domains.Entities.AccessManagement;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// Regression coverage for AccountController passing unawaited service Tasks to its views after the
// Core services became async: each endpoint below used to fail with a 500 (the view expected a List,
// not a Task). They must render their view/partial with the awaited data.
public sealed class AccountAccessEndpointsRegressionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AccountAccessEndpointsRegressionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record Seeded(HttpClient Client, string UserId, string Email, int ApplicationId,
        Sector Sector, SectorEntity Entity, EntityAccess Access);

    private async Task<Seeded> SeedAsync()
    {
        var email = $"account-access-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sector = new Sector { ApplicationId = applicationId, Title = $"Sector {Guid.NewGuid():N}", IsActive = true };
        context.Add(sector);
        await context.SaveChangesAsync();
        var entity = new SectorEntity { SectorId = sector.Id, Title = "Pages", AccessKey = $"KEY_{Guid.NewGuid():N}", IsActive = true };
        context.Add(entity);
        await context.SaveChangesAsync();
        var access = new EntityAccess { EntityId = entity.Id, Access = $"ACCESS_{Guid.NewGuid():N}", IsActive = true };
        context.Add(access);
        await context.SaveChangesAsync();

        return new Seeded(client, user.Id, email, applicationId, sector, entity, access);
    }

    private static async Task<string> AssertRenderedAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("System.Threading.Tasks", body);
        return body;
    }

    [Fact]
    public async Task UserList_WithEmail_RendersMatchingUsers()
    {
        var s = await SeedAsync();

        var body = await AssertRenderedAsync(await s.Client.PostAsync("/BackOffice/Account/UserList",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["IsAdminUser"] = "true", ["Email"] = s.Email })));

        Assert.Contains(s.Email, body);
        Assert.Contains($"userSettingForm('{s.UserId}')", body);
    }

    [Fact]
    public async Task UserSettingForm_RendersRolesAndApplications()
    {
        var s = await SeedAsync();

        var body = await AssertRenderedAsync(await s.Client.GetAsync($"/BackOffice/Account/UserSettingForm/{s.UserId}"));

        Assert.Contains("id=\"roleTabTitle\"", body);
        Assert.Contains("SuperAdmin", body);
        Assert.Contains($"data-focus-key=\"app-{s.ApplicationId}\"", body);
    }

    [Fact]
    public async Task Sectors_RendersSectorsGroupedByApplication()
    {
        var s = await SeedAsync();

        var body = await AssertRenderedAsync(await s.Client.GetAsync($"/BackOffice/Account/Sectors/{s.UserId}"));

        Assert.Contains(s.Sector.Title, body);
    }

    [Fact]
    public async Task Entities_RendersTheUsersApplicationsAsOptions()
    {
        var s = await SeedAsync();

        var body = await AssertRenderedAsync(await s.Client.GetAsync($"/BackOffice/Account/Entities/{s.UserId}"));

        Assert.Contains("id=\"frmSaveEntitiesToUser\"", body);
        Assert.Contains($"<option value=\"{s.ApplicationId}\">", body);
    }

    [Fact]
    public async Task GetApplicationSectors_RendersSectorOptions()
    {
        var s = await SeedAsync();

        var body = await AssertRenderedAsync(await s.Client.GetAsync($"/BackOffice/Account/GetApplicationSectors/{s.ApplicationId}"));

        Assert.Contains($"<option value=\"{s.Sector.Id}\">{s.Sector.Title}</option>", body);
    }

    [Fact]
    public async Task GetSectorEntities_RendersEntityChoices()
    {
        var s = await SeedAsync();

        var body = await AssertRenderedAsync(await s.Client.GetAsync($"/BackOffice/Account/GetSectorEntities/{s.Sector.Id}"));

        Assert.Contains($"onclick=\"loadEntityAccesses('{s.Entity.AccessKey}', '{s.Entity.Id}')\"", body);
    }

    [Fact]
    public async Task EntityAccesses_RendersAccessCheckboxes()
    {
        var s = await SeedAsync();

        var body = await AssertRenderedAsync(await s.Client.GetAsync($"/BackOffice/Account/EntityAccesses/{s.Entity.Id}"));

        Assert.Contains($"id=\"{s.Access.Access}\"", body);
        Assert.Contains($"onchange=\"setEntityAccessHideInput('{s.Access.Access}')\"", body);
    }
}
