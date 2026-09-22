using Microsoft.AspNetCore.Authorization;
using Web.Areas.BackOffice.Controllers;
using Xunit;

namespace Core.Tests.Architecture;

// Application is the tenant root: creating, updating (logo upload also regenerates the
// application key) or deleting one must never be reachable by authentication alone. This
// proves the declarative gate ASP.NET Core's authorization middleware actually enforces - a
// non-SuperAdmin is denied before the action body (and therefore any lookup/mutation) runs,
// while ordinary members keep the read/selection actions they need (list, select, enter).
public class ApplicationRootAdministrationAuthorizationTests
{
    private const string SuperAdminRole = "SuperAdmin";

    private static readonly string[] RootAdministrationActions =
    {
        nameof(ApplicationController.SaveApplicationForm),
        nameof(ApplicationController.UploadApplicationLogo),
    };

    private static readonly string[] OrdinaryMemberActions =
    {
        nameof(ApplicationController.SelectApp),
        nameof(ApplicationController.ApplicationForm),
        nameof(ApplicationController.SelectAppToEnter),
    };

    [Theory]
    [MemberData(nameof(RootAdministrationActionNames))]
    public void RootAdministrationAction_RequiresSuperAdminRole(string actionName)
    {
        var method = typeof(ApplicationController).GetMethod(actionName);

        var requiresSuperAdmin = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Any(a => a.Roles == SuperAdminRole);

        Assert.True(requiresSuperAdmin, $"{actionName} must carry [Authorize(Roles = \"SuperAdmin\")].");
    }

    [Theory]
    [MemberData(nameof(OrdinaryMemberActionNames))]
    public void OrdinaryMemberAction_DoesNotRequireASpecificRole(string actionName)
    {
        var method = typeof(ApplicationController).GetMethod(actionName);

        var restrictsToARole = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Any(a => !string.IsNullOrEmpty(a.Roles));

        Assert.False(restrictsToARole, $"{actionName} is used by ordinary members and must stay reachable by any authenticated, approved user.");
    }

    public static IEnumerable<object[]> RootAdministrationActionNames() => RootAdministrationActions.Select(a => new object[] { a });
    public static IEnumerable<object[]> OrdinaryMemberActionNames() => OrdinaryMemberActions.Select(a => new object[] { a });
}
