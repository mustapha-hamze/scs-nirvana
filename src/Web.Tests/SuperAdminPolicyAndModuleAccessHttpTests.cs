using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Web.Tests;

// Real HTTP behavioral proof for the two Auth kinds the matrix in
// BackOfficeEndpointAuthorizationMatrixTests only checks by attribute reflection and that
// AccessKeyAuthorizationTests doesn't already cover end to end: the SuperAdminPolicy rows
// (General, AccessManagement) and the AccessKey rows for Category/Schema's single, controller-wide
// module key.
public sealed class SuperAdminPolicyAndModuleAccessHttpTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SuperAdminPolicyAndModuleAccessHttpTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task General_OrdinaryMember_IsDeniedTheSuperAdminPolicy()
    {
        var email = $"policy-general-ordinary-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/General/Tags");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task General_SuperAdmin_IsAllowedByTheSuperAdminPolicy()
    {
        var email = $"policy-general-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/General/Tags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AccessManagement_OrdinaryMember_IsDeniedTheSuperAdminPolicy()
    {
        var email = $"policy-am-ordinary-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/AccessManagement/Sectors");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccessManagement_SuperAdmin_IsAllowedByTheSuperAdminPolicy()
    {
        var email = $"policy-am-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/AccessManagement/Sectors");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Category_OrdinaryMemberWithoutModuleKey_IsForbidden()
    {
        var email = $"access-category-noperm-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Category/Index");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Category_ExactModuleKey_IsAllowed()
    {
        var email = $"access-category-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Category.Module);

        var response = await client.GetAsync("/BackOffice/Category/List");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Category_SuperAdmin_IsAllowed()
    {
        var email = $"access-category-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Category/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Schema_OrdinaryMemberWithoutModuleKey_IsForbidden()
    {
        var email = $"access-schema-noperm-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Schema/Index");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Schema_ExactModuleKey_IsAllowed()
    {
        var email = $"access-schema-exact-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, Web.Authorization.AccessKeys.Schema.Module);

        var response = await client.GetAsync("/BackOffice/Schema/SchemaList");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Schema_SuperAdmin_IsAllowed()
    {
        var email = $"access-schema-superadmin-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Schema/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
