using System;
using System.Net;
using System.Net.Http;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// Regression coverage for GET /BackOffice/Application/SelectApp: ApplicationController.SelectApp
// used to pass the unawaited Task<List<ApplicationDto>> from IApplicationServices.List straight
// into View(...), while the view declares @model List<ApplicationDto> - a real, live HTTP request
// hits the actual model/view type mismatch this way, which a signature-only reflection check would
// never surface (the controller's return type is just Task<IActionResult> either way).
public sealed class SelectAppRenderingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SelectAppRenderingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SelectApp_ApprovedUserWithMembership_Returns200WithApplicationContent()
    {
        var email = $"select-app-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");

        var applicationTitle = $"Selectable App {Guid.NewGuid():N}";
        int applicationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var application = new Domains.Entities.General.Application { Title = applicationTitle, IsActive = true };
            context.Applications.Add(application);
            await context.SaveChangesAsync();
            applicationId = application.Id;

            context.UserInApplications.Add(new UserInApplication { UserId = user.Id, ApplicationId = applicationId, IsActive = true });
            await context.SaveChangesAsync();
        }

        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var response = await client.GetAsync("/BackOffice/Application/SelectApp");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Select application to enter admin panel", body);
        Assert.Contains(applicationTitle, body);
        Assert.Contains($"/BackOffice/Application/SelectAppToEnter/{applicationId}", body);
    }
}
