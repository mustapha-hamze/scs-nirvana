namespace Application.Contracts.Tenancy
{
    // A selected tenant id (see ICurrentApplicationContext) is a selector, not a grant - it must
    // be revalidated against live membership/application state on every protected request, since
    // either can change (revoked membership, deactivated/deleted application) after the id was
    // stored. Delivery-agnostic: no HttpContext/session/claims here - the caller (which does have
    // access to the authenticated principal) passes the identified user's email and, explicitly,
    // whether that principal holds the SuperAdmin role. This is the single boundary both
    // selection-time (UserManagementServices.SetCurrentApplicationId) and per-request
    // (RequireTenantContextFilter) tenant checks share, so the rule lives in exactly one place.
    public interface ITenantAccessGuard
    {
        // isSuperAdmin lets a SuperAdmin access any active, non-deleted application without an
        // UserInApplication row of their own; it still requires the application itself to be
        // active/non-deleted. Defaults to false so every existing (non-SuperAdmin-aware) caller
        // keeps today's membership-only behavior unchanged.
        Task<bool> HasAccessAsync(string email, int applicationId, bool isSuperAdmin = false, CancellationToken cancellationToken = default);
    }
}
