using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Web.Areas.BackOffice.Controllers;
using Xunit;

namespace Core.Tests.Architecture;

// Role administration (listing/creating roles, granting/revoking a role - including SuperAdmin
// itself) is privileged, global administration: authentication plus a selected-tenant membership
// must never be enough. The "User Management" sidebar already hides this from non-SuperAdmins
// client-side; these tests prove the server actually enforces it too.
public class RoleAdministrationAuthorizationTests
{
    private static readonly MethodInfo RolesGet = typeof(AccountController).GetMethod(nameof(AccountController.Roles), Type.EmptyTypes);
    private static readonly MethodInfo RolesPost = typeof(AccountController).GetMethod(nameof(AccountController.Roles), new[] { typeof(string) });
    private static readonly MethodInfo AddUserToRole = typeof(AccountController).GetMethod(nameof(AccountController.AddUserToRole));
    private static readonly MethodInfo RemoveUserFromRole = typeof(AccountController).GetMethod(nameof(AccountController.RemoveUserFromRole));

    public static IEnumerable<object[]> RoleMutatingActions() =>
        new[] { RolesGet, RolesPost, AddUserToRole, RemoveUserFromRole }.Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(RoleMutatingActions))]
    public async Task OrdinaryMember_IsDenied(MethodInfo method)
    {
        Assert.False(await AuthorizeRoleTestHelper.SatisfiesAsync(method, AuthorizeRoleTestHelper.OrdinaryMember()));
    }

    [Theory]
    [MemberData(nameof(RoleMutatingActions))]
    public async Task SuperAdmin_IsAllowed(MethodInfo method)
    {
        Assert.True(await AuthorizeRoleTestHelper.SatisfiesAsync(method, AuthorizeRoleTestHelper.SuperAdmin()));
    }

    [Fact]
    public void RoleAndUserToRoleEndpoints_RequireSuperAdmin()
    {
        // Reflection-level restatement of the behavioral checks above, so a missing attribute
        // shows up even if SatisfiesAsync's evaluation logic itself ever changes.
        foreach (var method in new[] { RolesGet, RolesPost, AddUserToRole, RemoveUserFromRole })
        {
            var requiresSuperAdmin = method.GetCustomAttributes<AuthorizeAttribute>(inherit: false).Any(a => a.Roles == "SuperAdmin");
            Assert.True(requiresSuperAdmin, $"{method.Name} must carry [Authorize(Roles = \"SuperAdmin\")].");
        }
    }

    [Fact]
    public void NoAlternateRoute_MutatesIdentityRoles()
    {
        // AddToRoleAsync/RemoveFromRoleAsync must only ever be called from the two gated actions
        // above (Program.cs's disabled-by-default SuperAdmin seed bootstrap is the one other,
        // reviewed exception). A source-level guard: reflection can't see method-body call sites,
        // so this scans src/Web for the literal call and fails if a new one shows up anywhere else.
        var webSourcePath = GetWebSourcePath();
        var sourceFiles = Directory.EnumerateFiles(webSourcePath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        var addCallSites = 0;
        var removeCallSites = 0;
        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            addCallSites += CountOccurrences(text, "AddToRoleAsync(");
            removeCallSites += CountOccurrences(text, "RemoveFromRoleAsync(");
        }

        // AccountController.AddUserToRole plus Program.cs's disabled-by-default seed bootstrap.
        Assert.True(addCallSites == 2, $"Expected exactly two AddToRoleAsync( call sites, found {addCallSites}.");
        Assert.True(removeCallSites == 1, $"Expected exactly one RemoveFromRoleAsync( call site (AccountController.RemoveUserFromRole), found {removeCallSites}.");
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static string GetWebSourcePath([CallerFilePath] string thisFilePath = "")
    {
        var srcDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "..", ".."));
        return Path.Combine(srcDir, "Web");
    }
}
