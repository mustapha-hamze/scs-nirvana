using System.Reflection;
using Core.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Web.Areas.BackOffice.Controllers;
using Web.Models.UserManagement;
using Xunit;

namespace Core.Tests.Architecture;

// User administration (enumerating every user, editing another user's profile, or flipping
// IsAdminUser/IsApprove) is privileged, global administration. CreateUserDto.IsAdminUser and
// IsApprove bind directly from the posted form with no further check, so the only thing standing
// between an ordinary member and self-approval/self-privilege-escalation is this role gate.
public class UserAdministrationAuthorizationTests
{
    private static readonly MethodInfo Users = typeof(AccountController).GetMethod(nameof(AccountController.Users));
    private static readonly MethodInfo UserForm = typeof(AccountController).GetMethod(nameof(AccountController.UserForm));
    private static readonly MethodInfo SaveUserForm = typeof(AccountController).GetMethod(nameof(AccountController.SaveUserForm));
    private static readonly MethodInfo UserList = typeof(AccountController).GetMethod(nameof(AccountController.UserList));

    public static IEnumerable<object[]> UserAdministrationActions() =>
        new[] { Users, UserForm, SaveUserForm, UserList }.Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(UserAdministrationActions))]
    public async Task OrdinaryMember_IsDenied(MethodInfo method)
    {
        Assert.False(await AuthorizeRoleTestHelper.SatisfiesAsync(method, AuthorizeRoleTestHelper.OrdinaryMember()));
    }

    [Theory]
    [MemberData(nameof(UserAdministrationActions))]
    public async Task SuperAdmin_IsAllowed(MethodInfo method)
    {
        Assert.True(await AuthorizeRoleTestHelper.SatisfiesAsync(method, AuthorizeRoleTestHelper.SuperAdmin()));
    }

    [Fact]
    public void UserAdministrationActions_RequireSuperAdmin()
    {
        foreach (var method in new[] { Users, UserForm, SaveUserForm, UserList })
        {
            var requiresSuperAdmin = method.GetCustomAttributes<AuthorizeAttribute>(inherit: false).Any(a => a.Roles == "SuperAdmin");
            Assert.True(requiresSuperAdmin, $"{method.Name} must carry [Authorize(Roles = \"SuperAdmin\")].");
        }
    }

    [Fact]
    public async Task SaveUserForm_CraftedSelfPrivilegeEscalationPayload_IsDeniedForOrdinaryMember()
    {
        // The exact attack this gate closes: an ordinary member posts a DTO that grants
        // themselves admin-user status and approval (or edits an arbitrary existing UserId with
        // the same flags). CreateUserDto.IsAdminUser/IsApprove bind straight from the request
        // with no further check, so the authorization gate - which runs before the action body
        // ever sees this DTO - is the only thing standing in the way.
        var craftedPayload = new CreateUserDto
        {
            UserId = "victim-or-self",
            EmailAddress = "attacker@example.com",
            FirstName = "A",
            LastName = "B",
            PhoneNumber = "00000000000",
            IsAdminUser = true,
            IsApprove = true,
        };

        // The authorization check runs before the action body, so it cannot see - and therefore
        // cannot be influenced by - craftedPayload's content. Denial holds regardless of it.
        Assert.True(craftedPayload.IsAdminUser && craftedPayload.IsApprove);
        Assert.False(await AuthorizeRoleTestHelper.SatisfiesAsync(SaveUserForm, AuthorizeRoleTestHelper.OrdinaryMember()));
    }
}
