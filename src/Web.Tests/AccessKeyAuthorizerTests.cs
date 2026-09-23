using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Application.Contracts.UserManagement;
using Application.UseCases.UserManagementServices;
using Web.Authorization;
using Xunit;

namespace Web.Tests;

// Unit coverage for AccessKeyAuthorizer's token-parsing boundaries: exact-token comparison must
// never degrade into the Contains/prefix matching the Razor views themselves use client-side
// (accesses.Contains("KEY")), and malformed/empty/null persisted values must deny rather than
// throw. No HTTP/DI host needed - a fake IUserManagementServices is enough to isolate the parsing
// logic.
public sealed class AccessKeyAuthorizerTests
{
    private sealed class FakeUserManagementServices : IUserManagementServices
    {
        private readonly string _accesses;
        public int GetUserAccessesCallCount;
        public FakeUserManagementServices(string accesses) => _accesses = accesses;

        public Task<List<UserDto>> List(bool isAdminUser, string email = "", CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();
        public Task<UserDto> GetUserByEmailAddress(string email, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();
        public Task<string> GetUserAccesses(string email, int appId, CancellationToken cancellationToken = default)
        {
            GetUserAccessesCallCount++;
            return Task.FromResult(_accesses);
        }
        public Task SetCurrentApplicationId(string email, int appId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();
        public Task SetUserAccesses(string accesses, string userId, int appId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();
    }

    private static ClaimsPrincipal MemberUser(bool superAdmin = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "member@test.local") };
        if (superAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public async Task ExactKey_IsGranted()
    {
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices("CMS1000_1001,CMS1000_1002"));
        Assert.True(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_1001"));
    }

    [Fact]
    public async Task PrefixOfAGrantedKey_IsDenied()
    {
        // A prefix/similar key must never grant a permission it wasn't issued - the exact bug
        // Contains(...) would introduce.
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices("CMS1000_1001"));
        Assert.False(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_10011"));
    }

    [Fact]
    public async Task GrantedKeyIsSuffixExtensionOfRequired_IsDenied()
    {
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices("CMS1000_1001_EDIT_1005_EXTRA"));
        Assert.False(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_1001_EDIT_1005"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task EmptyNullOrWhitespaceAccesses_IsDenied(string? accesses)
    {
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices(accesses!));
        Assert.False(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_1001"));
    }

    [Fact]
    public async Task MalformedCommaSeparatedValue_DoesNotThrowAndParsesRealTokens()
    {
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices(",,CMS1000_1001,, ,"));
        Assert.True(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_1001"));
        Assert.False(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_9999"));
    }

    [Fact]
    public async Task WhitespaceAroundTokens_IsTrimmedBeforeComparison()
    {
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices(" CMS1000_1001 , CMS1000_1002 "));
        Assert.True(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_1001"));
    }

    [Fact]
    public async Task SuperAdmin_BypassesEvenWithNoGrantedAccesses()
    {
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices(""));
        Assert.True(await authorizer.HasAccessAsync(MemberUser(superAdmin: true), 1, "CMS1000_1001"));
    }

    [Fact]
    public async Task AnyOfMultipleRequiredKeys_MatchesEitherOne()
    {
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices("SCM3000_1001_UPDATE_1005"));
        Assert.True(await authorizer.HasAccessAsync(MemberUser(), 1, "SCM3000_1001_SAVEITEM_1002", "SCM3000_1001_UPDATE_1005"));
    }

    [Fact]
    public async Task NoKeysRequested_IsDenied()
    {
        var authorizer = new AccessKeyAuthorizer(new FakeUserManagementServices("CMS1000_1001"));
        Assert.False(await authorizer.HasAccessAsync(MemberUser(), 1));
    }

    // Web Phase 4 fix: AccessKeyAuthorizer is registered Scoped (one instance per HTTP request),
    // so caching the token fetch on the instance is what makes RequireAccessAttribute and every
    // BackOfficeShellContext presentation read in the same request share one GetUserAccesses call.
    [Fact]
    public async Task RepeatedChecks_OnTheSameInstance_FetchAccessesOnlyOnce()
    {
        var fake = new FakeUserManagementServices("CMS1000_1001,CMS1000_1002");
        var authorizer = new AccessKeyAuthorizer(fake);

        Assert.True(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_1001"));
        Assert.True(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_1002"));
        Assert.False(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_9999"));

        Assert.Equal(1, fake.GetUserAccessesCallCount);
    }

    [Fact]
    public async Task SuperAdmin_NeverFetchesPersistedAccesses()
    {
        var fake = new FakeUserManagementServices("");
        var authorizer = new AccessKeyAuthorizer(fake);

        Assert.True(await authorizer.HasAccessAsync(MemberUser(superAdmin: true), 1, "CMS1000_1001"));
        Assert.True(await authorizer.HasAccessAsync(MemberUser(superAdmin: true), 1, "SCM3000_1001"));

        Assert.Equal(0, fake.GetUserAccessesCallCount);
    }

    [Fact]
    public async Task DifferentApplicationId_RefetchesInsteadOfReusingTheWrongTenantsCache()
    {
        var fake = new FakeUserManagementServices("CMS1000_1001");
        var authorizer = new AccessKeyAuthorizer(fake);

        Assert.True(await authorizer.HasAccessAsync(MemberUser(), 1, "CMS1000_1001"));
        Assert.True(await authorizer.HasAccessAsync(MemberUser(), 2, "CMS1000_1001"));

        Assert.Equal(2, fake.GetUserAccessesCallCount);
    }
}
