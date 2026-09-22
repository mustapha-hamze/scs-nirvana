using System.Net;
using System.Net.Http;
using System.Threading;
using Application.UseCases.UserManagementServices;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Web.Tests;

// Real rendered-page coverage for the BackOffice shared shell (Web Phase 4 task 2) - proves the
// presentation boundary (Web/Areas/BackOffice/Presentation/Shell/BackOfficeShellContext.cs) drives
// the exact same navigation visibility, selected-application display, and logout markup the old
// per-partial service calls did, while resolving the user/application/access snapshot at most once
// per rendered request.
public sealed class BackOfficeShellRenderingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public BackOfficeShellRenderingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OrdinaryTenantMember_SeesNoRestrictedNavigation()
    {
        var email = $"shell-ordinary-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var body = await GetHomeIndexAsync(client);

        Assert.DoesNotContain("id=\"cmsMenuLi\"", body);
        Assert.DoesNotContain("id=\"liCMSContent\"", body);
        Assert.DoesNotContain("id=\"sidebarSCM\"", body);
        Assert.DoesNotContain("id=\"sidebarUserManagement\"", body);
        Assert.DoesNotContain("id=\"sidebarGeneral\"", body);
        Assert.DoesNotContain("Change Application", body);
    }

    [Fact]
    public async Task ExactKeyMember_SeesOnlyGrantedNavigation()
    {
        var email = $"shell-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId,
            $"{Web.Authorization.AccessKeys.Content.Module},{Web.Authorization.AccessKeys.Slider.Module}");

        var body = await GetHomeIndexAsync(client);

        Assert.Contains("id=\"liCMSContent\"", body);
        Assert.Contains("id=\"liSCMSlider\"", body);
        Assert.DoesNotContain("id=\"liCMSAppPages\"", body);
        Assert.DoesNotContain("id=\"liCMSCategory\"", body);
        Assert.DoesNotContain("id=\"liCMSSchema\"", body);
        Assert.DoesNotContain("id=\"sidebarUserManagement\"", body);
        Assert.DoesNotContain("id=\"sidebarGeneral\"", body);
    }

    [Fact]
    public async Task PrefixKeyMember_DoesNotSeeContentLink()
    {
        var email = $"shell-prefix-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        // Shares "CMS1000_1001" as a string prefix but is a different key - the same false
        // positive AccessKeyAuthorizationTests' Content_SimilarPrefixPermission_ReadIsStillDenied
        // proves server-side. The old accesses.Contains("CMS1000_1001") check would wrongly show
        // the Content link for this key; HasModuleAccess must not.
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, "CMS1000_10011");

        var body = await GetHomeIndexAsync(client);

        Assert.Contains("id=\"cmsMenuLi\"", body); // still in the CMS family (starts with "CMS1000_")
        Assert.DoesNotContain("id=\"liCMSContent\"", body);
    }

    [Fact]
    public async Task SuperAdmin_SeesEveryNavigationSection()
    {
        var email = $"shell-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var body = await GetHomeIndexAsync(client);

        Assert.Contains("id=\"liCMSContent\"", body);
        Assert.Contains("id=\"liSCMSlider\"", body);
        Assert.Contains("id=\"sidebarUserManagement\"", body);
        Assert.Contains("id=\"sidebarGeneral\"", body);
    }

    [Fact]
    public async Task SelectedApplication_TitleIsDisplayedInSidebar()
    {
        var email = $"shell-apptitle-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var title = await ApplicationTitleAsync(applicationId);

        var body = await GetHomeIndexAsync(client);

        Assert.Contains($"<h5>{title}</h5>", body);
    }

    [Fact]
    public async Task SingleApplicationMember_HidesChangeApplicationLink()
    {
        var email = $"shell-singleapp-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var body = await GetHomeIndexAsync(client);

        Assert.DoesNotContain("Change Application", body);
    }

    [Fact]
    public async Task MultipleApplicationMember_ShowsChangeApplicationLink()
    {
        var email = $"shell-multiapp-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await SeedSecondMembershipAsync(user.Id);

        var body = await GetHomeIndexAsync(client);

        Assert.Contains("Change Application", body);
    }

    [Fact]
    public async Task LogoutMarkup_IsUnchanged()
    {
        var email = $"shell-logout-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var body = await GetHomeIndexAsync(client);

        Assert.Contains("<form method=\"post\" action=\"/Logout\" class=\"m-0\">", body);
        Assert.Contains("name=\"__RequestVerificationToken\"", body);
        Assert.Contains("<i class=\"mdi mdi-logout me-1\"></i>", body);
    }

    [Fact]
    public async Task BackOfficeShellSnapshot_ResolvesUserAndAccessesOnlyOncePerRenderedRequest()
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

        var email = $"shell-dupcheck-{Guid.NewGuid():N}@test.local";
        // SuperAdmin so the request needs no separately-granted access key - this test is about
        // call counts, not permission classification.
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(counterFactory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(counterFactory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(counterFactory, client, user);
        var counter = counterFactory.Services.GetRequiredService<CallCounter>();
        counter.Reset();

        // Category/Index has no view-level calls of its own to these services (unlike Home/Index,
        // which - outside this task's Views/Shared scope - still makes its own separate calls) so
        // every call below is attributable to the shared shell (_Navbar/_SideBar/_SideBarCMS/
        // _SideBarSCM) alone.
        var response = await client.GetAsync("/BackOffice/Category/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, counter.GetUserAccessesCalls);
        Assert.Equal(1, counter.GetUserByEmailAddressCalls);
    }

    private async Task<string> GetHomeIndexAsync(HttpClient client)
    {
        var response = await client.GetAsync("/BackOffice/Home/Index");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> ApplicationTitleAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var application = await context.Applications.FindAsync(applicationId);
        return application!.Title;
    }

    private async Task SeedSecondMembershipAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondApp = new Domains.Entities.General.Application { Title = $"Second App {Guid.NewGuid():N}", IsActive = true };
        context.Applications.Add(secondApp);
        await context.SaveChangesAsync();
        context.UserInApplications.Add(new UserInApplication { UserId = userId, ApplicationId = secondApp.Id, IsActive = true });
        await context.SaveChangesAsync();
    }

    private sealed class CallCounter
    {
        public int GetUserAccessesCalls;
        public int GetUserByEmailAddressCalls;

        public void Reset()
        {
            GetUserAccessesCalls = 0;
            GetUserByEmailAddressCalls = 0;
        }
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

        public Task<Application.Contracts.UserManagement.UserDto> GetUserByEmailAddress(string email, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _counter.GetUserByEmailAddressCalls);
            return _inner.GetUserByEmailAddress(email, cancellationToken);
        }

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
