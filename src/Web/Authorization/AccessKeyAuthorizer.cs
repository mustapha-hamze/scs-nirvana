using System.Security.Claims;

namespace Web.Authorization;

// Server-side enforcement of the tenant-scoped BackOffice access-key contract that the Razor
// views already gate on client-side (accesses.Contains("KEY")). Unlike Contains, token matching
// here is exact - a persisted "CMS1000_1001_EDIT_1005" must never satisfy a check for
// "CMS1000_1001_EDIT_1005_EXTRA" or vice versa, and a prefix/similar key must never grant a
// permission it wasn't issued. SuperAdmin always bypasses the check. Nothing here is cached
// across requests - every call re-reads the persisted access value for the current request.
public sealed class AccessKeyAuthorizer
{
    private readonly IUserManagementServices _userManagementServices;

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

        var raw = await _userManagementServices.GetUserAccesses(email, applicationId);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var key in anyOfKeys)
        {
            if (Array.IndexOf(tokens, key) >= 0)
                return true;
        }

        return false;
    }
}
