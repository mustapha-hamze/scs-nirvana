using Xunit;
using Web.Areas.BackOffice.Presentation.Shell;

namespace Web.Tests;

// Focused unit coverage for BackOfficeAccessSnapshot (Web Phase 4 task 2) - the exact-token
// replacement for the Shared views' old accesses.Contains(prefix) gates. Proves the same
// SuperAdmin bypass and exact-token semantics as AccessKeyAuthorizer/RequireAccessAttribute
// server-side (Web Phase 3), including the specific prefix-key false positive
// (AccessKeyAuthorizationTests' Content_SimilarPrefixPermission_ReadIsStillDenied proves the same
// bug class server-side) that Contains used to let through.
public sealed class BackOfficeAccessSnapshotTests
{
    private static BackOfficeAccessSnapshot Snapshot(bool isSuperAdmin, params string[] tokens) =>
        new(isSuperAdmin, new HashSet<string>(tokens));

    [Fact]
    public void HasModuleAccess_ExactToken_IsTrue()
    {
        var access = Snapshot(false, "CMS1000_1001");
        Assert.True(access.HasModuleAccess("CMS1000_1001"));
    }

    [Fact]
    public void HasModuleAccess_SubActionToken_IsTrue()
    {
        var access = Snapshot(false, "CMS1000_1001_ADD_1000");
        Assert.True(access.HasModuleAccess("CMS1000_1001"));
    }

    [Fact]
    public void HasModuleAccess_SimilarPrefixToken_IsFalse()
    {
        // "CMS1000_10011" shares "CMS1000_1001" as a string prefix but is a different key - the
        // exact false positive Contains("CMS1000_1001") used to let through.
        var access = Snapshot(false, "CMS1000_10011");
        Assert.False(access.HasModuleAccess("CMS1000_1001"));
    }

    [Fact]
    public void HasModuleAccess_UnrelatedToken_IsFalse()
    {
        var access = Snapshot(false, "SCM3000_1001");
        Assert.False(access.HasModuleAccess("CMS1000_1001"));
    }

    [Fact]
    public void HasModuleAccess_SuperAdmin_IsTrueRegardlessOfTokens()
    {
        var access = Snapshot(true);
        Assert.True(access.HasModuleAccess("CMS1000_1001"));
    }

    [Fact]
    public void HasFamilyAccess_TokenInFamily_IsTrue()
    {
        var access = Snapshot(false, "CMS1000_1002");
        Assert.True(access.HasFamilyAccess(Web.Authorization.AccessKeys.ContentManagementFamily));
    }

    [Fact]
    public void HasFamilyAccess_TokenOutsideFamily_IsFalse()
    {
        var access = Snapshot(false, "SCM3000_1001");
        Assert.False(access.HasFamilyAccess(Web.Authorization.AccessKeys.ContentManagementFamily));
    }

    [Fact]
    public void HasFamilyAccess_SuperAdmin_IsTrueRegardlessOfTokens()
    {
        var access = Snapshot(true);
        Assert.True(access.HasFamilyAccess(Web.Authorization.AccessKeys.MediaAndModulesFamily));
    }
}
