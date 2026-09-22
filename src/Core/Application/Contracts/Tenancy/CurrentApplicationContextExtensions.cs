namespace Application.Contracts.Tenancy
{
    public static class CurrentApplicationContextExtensions
    {
        // Every BackOffice tenant-scoped call site reads the selected tenant through here instead
        // of an inline "?? 0" fallback - application id 0 must never stand in for "no tenant
        // selected". Throwing means a call site that somehow runs without the request-time tenant
        // guard (RequireTenantContextFilter) having already validated the selection fails loudly
        // instead of silently querying application 0.
        public static int RequireApplicationId(this ICurrentApplicationContext context)
        {
            return context.CurrentApplicationId
                ?? throw new InvalidOperationException(
                    "No tenant is selected for the current request. Tenant-scoped code must run behind the request-time tenant guard.");
        }
    }
}
