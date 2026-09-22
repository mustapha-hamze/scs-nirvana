namespace Application.Contracts.Tenancy
{
    // A selected tenant id (see ICurrentApplicationContext) is a selector, not a grant - it must
    // be revalidated against live membership/application state on every protected request, since
    // either can change (revoked membership, deactivated/deleted application) after the id was
    // stored. Delivery-agnostic: no HttpContext/session/claims here, callers pass the identified
    // user's email explicitly.
    public interface ITenantAccessGuard
    {
        Task<bool> HasAccessAsync(string email, int applicationId, CancellationToken cancellationToken = default);
    }
}
