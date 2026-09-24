using Xunit;
using Web.Areas.BackOffice.Presentation.Shell;

namespace Web.Tests;

// Focused unit coverage for BackOfficeAccessSnapshot (Web Phase 4, revised by the Phase 4 fix
// pass) - the exact-token replacement for the Shared views' old accesses.Contains(prefix)/
// StartsWith gates. CanAccess/CanAnyAccess use exactly the same comparison as
// AccessKeyAuthorizer.HasAccessAsync server-side (exact token match, no sub-key/family
// broadening), so a link is never shown for a permission that would 403 at its destination - see
// AccessKeyAuthorizationTests' Content_SimilarPrefixPermission_ReadIsStillDenied for the
// server-side proof of the same "similar key" bug class.
public sealed class BackOfficeAccessSnapshotTests
{
    private static BackOfficeAccessSnapshot Snapshot(bool isSuperAdmin, params string[] tokens) =>
        new(isSuperAdmin, new HashSet<string>(tokens));

    [Fact]
    public void CanAccess_ExactToken_IsTrue()
    {
        var access = Snapshot(false, "CMS1000_1001");
        Assert.True(access.CanAccess("CMS1000_1001"));
    }

    [Fact]
    public void CanAccess_SubActionTokenAlone_IsFalse()
    {
        // Holding only a granular sub-action key must never make the module-gated destination
        // action (which requires the exact module key) appear reachable.
        var access = Snapshot(false, "CMS1000_1001_ADD_1000");
        Assert.False(access.CanAccess("CMS1000_1001"));
    }

    [Fact]
    public void CanAccess_SimilarPrefixToken_IsFalse()
    {
        // "CMS1000_10011" shares "CMS1000_1001" as a string prefix but is a different key - the
        // exact false positive Contains("CMS1000_1001") used to let through.
        var access = Snapshot(false, "CMS1000_10011");
        Assert.False(access.CanAccess("CMS1000_1001"));
    }

    [Fact]
    public void CanAccess_UnrelatedToken_IsFalse()
    {
        var access = Snapshot(false, "SCM3000_1001");
        Assert.False(access.CanAccess("CMS1000_1001"));
    }

    [Fact]
    public void CanAccess_SuperAdmin_IsTrueRegardlessOfTokens()
    {
        var access = Snapshot(true);
        Assert.True(access.CanAccess("CMS1000_1001"));
    }

    [Fact]
    public void CanAnyAccess_AnyMatchingExactKey_IsTrue()
    {
        var access = Snapshot(false, "CMS1000_1002");
        Assert.True(access.CanAnyAccess("CMS1000_1001", "CMS1000_1002", "CMS1000_1003"));
    }

    [Fact]
    public void CanAnyAccess_OnlySubActionKeys_IsFalse()
    {
        var access = Snapshot(false, "CMS1000_1001_ADD_1000");
        Assert.False(access.CanAnyAccess("CMS1000_1001", "CMS1000_1002", "CMS1000_1003"));
    }

    [Fact]
    public void CanAnyAccess_NoMatchingKey_IsFalse()
    {
        var access = Snapshot(false, "SCM3000_1001");
        Assert.False(access.CanAnyAccess("CMS1000_1001", "CMS1000_1002", "CMS1000_1003"));
    }

    [Fact]
    public void CanAnyAccess_SuperAdmin_IsTrueRegardlessOfTokens()
    {
        var access = Snapshot(true);
        Assert.True(access.CanAnyAccess("SCM3000_1001"));
    }
}
