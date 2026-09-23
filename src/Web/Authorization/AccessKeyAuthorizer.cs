using System.Security.Claims;

namespace Web.Authorization;

// Server-side enforcement of the tenant-scoped BackOffice access-key contract that the Razor
// views also gate on (BackOfficeAccessSnapshot.CanAccess/CanAnyAccess - see
// Web/Areas/BackOffice/Presentation/Shell/BackOfficeShellContext.cs). Token matching here is
// exact - a persisted "CMS1000_1001_EDIT_1005" must never satisfy a check for
// "CMS1000_1001_EDIT_1005_EXTRA" or vice versa, and a prefix/similar key must never grant a
// permission it wasn't issued. SuperAdmin always bypasses the check (and never triggers a
// persisted-access lookup at all).
//
// This type is registered Scoped, so one instance already exists per HTTP request - the token
// fetch below is cached on that instance (keyed to the validated (email, applicationId) pair, which
// is always sourced from the authenticated principal/ICurrentApplicationContext, never request
// input) so every RequireAccessAttribute check and every BackOfficeShellContext presentation read
// in the same request shares one GetUserAccesses call instead of issuing its own. Never cached
// across requests - a new instance is resolved for the next one.
public sealed class AccessKeyAuthorizer
{
    private readonly IUserManagementServices _userManagementServices;
    private (string Email, int ApplicationId)? _cachedKey;
    private Task<HashSet<string>> _cachedTokens;

    public AccessKeyAuthorizer(IUserManagementServices userManagementServices)
    {
        _userManagementServices = userManagementServices;
    }

    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, int applicationId, params string[] anyOfKeys)
    {
        if (user.IsInRole(ApplicationRoles.SuperAdmin))
            return true;

        if (anyOfKeys is null || anyOfKeys.Length == 0)
            return false;

        var email = user.Identity?.Name;
        if (string.IsNullOrEmpty(email))
            return false;

        var tokens = await GetTokensAsync(email, applicationId);
        foreach (var key in anyOfKeys)
        {
            if (tokens.Contains(key))
                return true;
        }

        return false;
    }

    // Exposes the same per-request-cached, exact-comparable token set HasAccessAsync itself uses,
    // for BackOfficeShellContext's presentation snapshot to read from - never a second
    // GetUserAccesses call for a request that already made one. Callers must check the SuperAdmin
    // bypass themselves before calling this (mirrors HasAccessAsync's own bypass-before-fetch
    // ordering); this method always performs/reuses the persisted-access fetch.
    internal Task<HashSet<string>> GetTokensAsync(string email, int applicationId)
    {
        var key = (email, applicationId);
        if (_cachedTokens is null || _cachedKey != key)
        {
            _cachedKey = key;
            _cachedTokens = FetchTokensAsync(email, applicationId);
        }

        return _cachedTokens;
    }

    private async Task<HashSet<string>> FetchTokensAsync(string email, int applicationId)
    {
        var raw = await _userManagementServices.GetUserAccesses(email, applicationId);
        return string.IsNullOrWhiteSpace(raw)
            ? new HashSet<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
    }
}
