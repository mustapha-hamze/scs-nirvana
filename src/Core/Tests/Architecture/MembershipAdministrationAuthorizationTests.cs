using System.Reflection;
using Core.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Web.Areas.BackOffice.Controllers;
using Xunit;

namespace Core.Tests.Architecture;

// Membership administration (adding/removing a user's application membership, and the
// cross-application sector/entity/access lookups the "Users" panel uses to manage them) is
// privileged, global administration - applicationId/appId/sectorId/entityId here are caller
// input, never authorization proof by themselves. These prove the SuperAdmin gate actually
// denies/allows the way ASP.NET Core's authorization middleware would.
public class MembershipAdministrationAuthorizationTests
{
    private static readonly MethodInfo AddUserToApplication = typeof(AccountController).GetMethod(nameof(AccountController.AddUserToApplication));
    private static readonly MethodInfo RemoveUserFromApplication = typeof(AccountController).GetMethod(nameof(AccountController.RemoveUserFromApplication));
    private static readonly MethodInfo UserSettingForm = typeof(AccountController).GetMethod(nameof(AccountController.UserSettingForm));
    private static readonly MethodInfo Sectors = typeof(AccountController).GetMethod(nameof(AccountController.Sectors));
    private static readonly MethodInfo Entities = typeof(AccountController).GetMethod(nameof(AccountController.Entities));
    private static readonly MethodInfo GetApplicationSectors = typeof(AccountController).GetMethod(nameof(AccountController.GetApplicationSectors));
    private static readonly MethodInfo GetUserAccess = typeof(AccountController).GetMethod(nameof(AccountController.GetUserAccess));
    private static readonly MethodInfo GetSectorEntities = typeof(AccountController).GetMethod(nameof(AccountController.GetSectorEntities));
    private static readonly MethodInfo EntityAccesses = typeof(AccountController).GetMethod(nameof(AccountController.EntityAccesses));

    public static IEnumerable<object[]> MembershipAdministrationActions() => new[]
    {
        AddUserToApplication, RemoveUserFromApplication, UserSettingForm, Sectors, Entities,
        GetApplicationSectors, GetUserAccess, GetSectorEntities, EntityAccesses,
    }.Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(MembershipAdministrationActions))]
    public async Task OrdinaryMember_IsDenied(MethodInfo method)
    {
        Assert.False(await AuthorizeRoleTestHelper.SatisfiesAsync(method, AuthorizeRoleTestHelper.OrdinaryMember()));
    }

    [Theory]
    [MemberData(nameof(MembershipAdministrationActions))]
    public async Task SuperAdmin_IsAllowed(MethodInfo method)
    {
        Assert.True(await AuthorizeRoleTestHelper.SatisfiesAsync(method, AuthorizeRoleTestHelper.SuperAdmin()));
    }

    [Fact]
    public void MembershipAdministrationActions_RequireSuperAdmin()
    {
        foreach (var method in new[]
        {
            AddUserToApplication, RemoveUserFromApplication, UserSettingForm, Sectors, Entities,
            GetApplicationSectors, GetUserAccess, GetSectorEntities, EntityAccesses,
        })
        {
            var requiresSuperAdmin = method.GetCustomAttributes<AuthorizeAttribute>(inherit: false).Any(a => a.Roles == "SuperAdmin");
            Assert.True(requiresSuperAdmin, $"{method.Name} must carry [Authorize(Roles = \"SuperAdmin\")].");
        }
    }

    [Fact]
    public void RemoveUserFromApplication_DoesNotAcceptACallerSuppliedApplicationId()
    {
        // The only int/string parameter is relationId - applicationId must come from
        // RequireApplicationId() (the caller's own validated selected tenant), never the route
        // or body. A second parameter here would be exactly the "trust the caller-supplied
        // applicationId" regression this action must never reintroduce.
        var parameters = RemoveUserFromApplication.GetParameters();
        var parameter = Assert.Single(parameters);
        Assert.Equal("relationId", parameter.Name);
    }
}
