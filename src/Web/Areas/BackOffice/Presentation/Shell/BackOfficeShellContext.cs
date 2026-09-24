using System.Security.Claims;
using Application.UseCases.GeneralServices;
using Application.UseCases.UserManagementServices;
using Application.UseCases.Utilities.ApplicationConst;
using Application.Contracts.Tenancy;
using Microsoft.AspNetCore.Http;
using Web.Authorization;

namespace Web.Areas.BackOffice.Presentation.Shell;

// Web Phase 4 task 2 (revised by the Phase 4 fix pass): the request-scoped, tenant-aware
// presentation boundary for the shared BackOffice shell (_Layout and its navbar/sidebar
// partials), and the single presentation-facing read of the current user's access tokens for
// Content/Slider Can* view-model flags. Nothing outside this file may call
// IUserManagementServices.GetUserAccesses, IApplicationServices.GetById, ISystemTypeServices, or
// ICurrentApplicationContext.RequireApplicationId from a shared Razor file - they call
// IBackOfficeShellContext.GetSnapshotAsync()/GetAccessSnapshotAsync() instead, which resolve at
// most once per request (cached on this scoped instance) and only when first asked - i.e. only
// after RequireTenantContextFilter has already validated the selected application, since shared
// views only render once a BaseController action executes.
//
// The access tokens themselves come from AccessKeyAuthorizer.GetTokensAsync, not a separate
// GetUserAccesses call - AccessKeyAuthorizer is the single per-request cache of that value (see
// its own doc comment), shared by every RequireAccessAttribute check and this presentation read.
// This snapshot is read-only display data derived from that cache; it is never the authorization
// authority - every actual allow/deny decision still runs through AccessKeyAuthorizer.HasAccessAsync.

public sealed record BackOfficeContentTypeLink(int Id, string Title);

public sealed record BackOfficeAppPageLink(string PageType, string Title);

// Exact AccessKeys token semantics - the same comparison AccessKeyAuthorizer uses server-side, not
// Razor's old accesses.Contains(prefix)/StartsWith. CanAccess/CanAnyAccess only ever return true
// for a key (or SuperAdmin) that would also satisfy the matching [RequireAccess] check server-side
// - callers must pass the exact key(s) that gate the link's destination, never a family/module
// prefix, so a link is never shown for a permission that would 403 (see
// AccessKeyAuthorizationTests' Content_SimilarPrefixPermission_ReadIsStillDenied for the
// server-side proof of the same "similar key" bug class this also avoids).
public sealed class BackOfficeAccessSnapshot
{
    private readonly IReadOnlySet<string> _tokens;

    public BackOfficeAccessSnapshot(bool isSuperAdmin, IReadOnlySet<string> tokens)
    {
        IsSuperAdmin = isSuperAdmin;
        _tokens = tokens;
    }

    public bool IsSuperAdmin { get; }

    public bool CanAccess(string key) => IsSuperAdmin || _tokens.Contains(key);

    public bool CanAnyAccess(params string[] keys) => IsSuperAdmin || keys.Any(_tokens.Contains);
}

public sealed record BackOfficeShellSnapshot(
    string UserDisplayName,
    bool IsSuperAdmin,
    int ApplicationId,
    string ApplicationTitle,
    bool HasMultipleApplications,
    BackOfficeAccessSnapshot Access,
    IReadOnlyList<BackOfficeContentTypeLink> ContentTypes,
    IReadOnlyList<BackOfficeAppPageLink> AppPages);

public interface IBackOfficeShellContext
{
    Task<BackOfficeShellSnapshot> GetSnapshotAsync();

    // Lightweight alternative to GetSnapshotAsync for callers that only need access-key flags
    // (e.g. Content/Slider Can* view-model fields) - skips the user/application/content-type/
    // app-page fetches GetSnapshotAsync also does, while still sharing the same underlying
    // AccessKeyAuthorizer token cache.
    Task<BackOfficeAccessSnapshot> GetAccessSnapshotAsync();
}

public sealed class BackOfficeShellContext : IBackOfficeShellContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserManagementServices _userManagementServices;
    private readonly IApplicationServices _applicationServices;
    private readonly ISystemTypeServices _systemTypeServices;
    private readonly ICurrentApplicationContext _currentApplicationContext;
    private readonly AccessKeyAuthorizer _accessKeyAuthorizer;
    private BackOfficeShellSnapshot _cachedSnapshot;
    private BackOfficeAccessSnapshot _cachedAccess;

    public BackOfficeShellContext(
        IHttpContextAccessor httpContextAccessor,
        IUserManagementServices userManagementServices,
        IApplicationServices applicationServices,
        ISystemTypeServices systemTypeServices,
        ICurrentApplicationContext currentApplicationContext,
        AccessKeyAuthorizer accessKeyAuthorizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManagementServices = userManagementServices;
        _applicationServices = applicationServices;
        _systemTypeServices = systemTypeServices;
        _currentApplicationContext = currentApplicationContext;
        _accessKeyAuthorizer = accessKeyAuthorizer;
    }

    private ClaimsPrincipal RequirePrincipal() =>
        _httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("BackOfficeShellContext requires an active HTTP request.");

    private static string RequireEmail(ClaimsPrincipal principal) =>
        principal.Identity?.Name
            ?? throw new InvalidOperationException("BackOfficeShellContext requires an authenticated user.");

    public async Task<BackOfficeAccessSnapshot> GetAccessSnapshotAsync()
    {
        if (_cachedAccess is not null)
            return _cachedAccess;

        var principal = RequirePrincipal();
        var isSuperAdmin = principal.IsInRole(ApplicationRoles.SuperAdmin);

        // SuperAdmin never needs a persisted-access lookup - CanAccess/CanAnyAccess short-circuit
        // on IsSuperAdmin before ever consulting the (here, empty) token set.
        var tokens = isSuperAdmin
            ? (IReadOnlySet<string>)new HashSet<string>()
            : await _accessKeyAuthorizer.GetTokensAsync(RequireEmail(principal), _currentApplicationContext.RequireApplicationId());

        _cachedAccess = new BackOfficeAccessSnapshot(isSuperAdmin, tokens);
        return _cachedAccess;
    }

    public async Task<BackOfficeShellSnapshot> GetSnapshotAsync()
    {
        if (_cachedSnapshot is not null)
            return _cachedSnapshot;

        var principal = RequirePrincipal();
        var email = RequireEmail(principal);
        var applicationId = _currentApplicationContext.RequireApplicationId();

        var access = await GetAccessSnapshotAsync();
        var user = await _userManagementServices.GetUserByEmailAddress(email);
        var application = await _applicationServices.GetById(applicationId);
        var userApplications = await _applicationServices.GetUserApplications(email);
        var contentTypes = await _systemTypeServices.GetTypesInTypeGroup(applicationId, TypeId.Content);
        var appPages = await _applicationServices.GetApplicationSetting(applicationId, 6000);

        _cachedSnapshot = new BackOfficeShellSnapshot(
            UserDisplayName: $"{user.FirstName} {user.LastName}",
            IsSuperAdmin: access.IsSuperAdmin,
            ApplicationId: applicationId,
            ApplicationTitle: application.Title,
            HasMultipleApplications: userApplications.Count > 1,
            Access: access,
            ContentTypes: contentTypes.Select(t => new BackOfficeContentTypeLink(t.Id, t.Title)).ToArray(),
            AppPages: appPages.OrderBy(p => p.Value).Select(p => new BackOfficeAppPageLink("100" + p.Value, p.Title)).ToArray());

        return _cachedSnapshot;
    }
}
