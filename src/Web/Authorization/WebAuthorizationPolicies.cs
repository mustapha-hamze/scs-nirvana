namespace Web.Authorization;

// Single, discoverable home for Web's authorization policy names, so controllers reference one
// named policy instead of scattering [Authorize(Roles = "SuperAdmin")] string literals. The
// policy itself (registered in ServiceCollectionExtensions.AddWebInfrastructure) still maps to
// Core's ApplicationRoles.SuperAdmin - this only names it for attribute use.
public static class WebAuthorizationPolicies
{
    public const string SuperAdmin = nameof(SuperAdmin);
}
