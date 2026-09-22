using Application.UseCases.GeneralServices;
using Application.UseCases.UserManagementServices;
using Application.UseCases.Utilities.ApplicationConst;
using Application.Contracts.Tenancy;
using Microsoft.AspNetCore.Http;
using Web.Authorization;

namespace Web.Areas.BackOffice.Presentation.Shell;

// Web Phase 4 task 2: the request-scoped, tenant-aware presentation boundary for the shared
// BackOffice shell (_Layout and its navbar/sidebar partials). Nothing outside this file may call
// IUserManagementServices.GetUserAccesses, IApplicationServices.GetById, ISystemTypeServices, or
// ICurrentApplicationContext.RequireApplicationId from a shared Razor file - they call
// IBackOfficeShellContext.GetSnapshotAsync() instead, which resolves everything at most once per
// request (cached on this scoped instance) and only when first asked - i.e. only after
// RequireTenantContextFilter has already validated the selected application, since shared views
// only render once a BaseController action executes.

public sealed record BackOfficeContentTypeLink(int Id, string Title);

public sealed record BackOfficeAppPageLink(string PageType, string Title);

// Exact AccessKeys token semantics - the same comparison AccessKeyAuthorizer uses server-side, not
// Razor's previous accesses.Contains(prefix). HasModuleAccess mirrors a module key's own real
// sub-action keys (e.g. Slider's key sharing AccessKeys.Content's CMS-prefix family, as documented
// in AccessKeys.cs); HasFamilyAccess mirrors a bare top-level family prefix. Neither ever matches
// a similar/prefix key across a token boundary the way Contains did (see
// AccessKeyAuthorizationTests' Content_SimilarPrefixPermission_ReadIsStillDenied for the server-side
// proof of the same bug class).
public sealed class BackOfficeAccessSnapshot
{
    private readonly bool _isSuperAdmin;
    private readonly HashSet<string> _tokens;

    public BackOfficeAccessSnapshot(bool isSuperAdmin, HashSet<string> tokens)
    {
        _isSuperAdmin = isSuperAdmin;
        _tokens = tokens;
    }

    public bool HasModuleAccess(string moduleKey) =>
        _isSuperAdmin || _tokens.Contains(moduleKey) || _tokens.Any(t => t.StartsWith(moduleKey + "_", StringComparison.Ordinal));

    public bool HasFamilyAccess(string familyPrefix) =>
        _isSuperAdmin || _tokens.Any(t => t.StartsWith(familyPrefix, StringComparison.Ordinal));
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
}

public sealed class BackOfficeShellContext : IBackOfficeShellContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserManagementServices _userManagementServices;
    private readonly IApplicationServices _applicationServices;
    private readonly ISystemTypeServices _systemTypeServices;
    private readonly ICurrentApplicationContext _currentApplicationContext;
    private BackOfficeShellSnapshot _cached;

    public BackOfficeShellContext(
        IHttpContextAccessor httpContextAccessor,
        IUserManagementServices userManagementServices,
        IApplicationServices applicationServices,
        ISystemTypeServices systemTypeServices,
        ICurrentApplicationContext currentApplicationContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManagementServices = userManagementServices;
        _applicationServices = applicationServices;
        _systemTypeServices = systemTypeServices;
        _currentApplicationContext = currentApplicationContext;
    }

    public async Task<BackOfficeShellSnapshot> GetSnapshotAsync()
    {
        if (_cached is not null)
            return _cached;

        var principal = _httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("BackOfficeShellContext requires an active HTTP request.");
        var email = principal.Identity?.Name
            ?? throw new InvalidOperationException("BackOfficeShellContext requires an authenticated user.");
        var applicationId = _currentApplicationContext.RequireApplicationId();
        var isSuperAdmin = principal.IsInRole(ApplicationRoles.SuperAdmin);

        var user = await _userManagementServices.GetUserByEmailAddress(email);
        var application = await _applicationServices.GetById(applicationId);
        var userApplications = await _applicationServices.GetUserApplications(email);
        var rawAccesses = await _userManagementServices.GetUserAccesses(email, applicationId);
        var tokens = (rawAccesses ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();
        var contentTypes = await _systemTypeServices.GetTypesInTypeGroup(applicationId, TypeId.Content);
        var appPages = await _applicationServices.GetApplicationSetting(applicationId, 6000);

        _cached = new BackOfficeShellSnapshot(
            UserDisplayName: $"{user.FirstName} {user.LastName}",
            IsSuperAdmin: isSuperAdmin,
            ApplicationId: applicationId,
            ApplicationTitle: application.Title,
            HasMultipleApplications: userApplications.Count > 1,
            Access: new BackOfficeAccessSnapshot(isSuperAdmin, tokens),
            ContentTypes: contentTypes.Select(t => new BackOfficeContentTypeLink(t.Id, t.Title)).ToArray(),
            AppPages: appPages.OrderBy(p => p.Value).Select(p => new BackOfficeAppPageLink("100" + p.Value, p.Title)).ToArray());

        return _cached;
    }
}
