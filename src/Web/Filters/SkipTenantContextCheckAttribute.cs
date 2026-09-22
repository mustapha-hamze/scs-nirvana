namespace Web.Filters;

// Marks a controller or action as legitimately reachable without a validated tenant selection:
// authentication, logout, and application-selection/root-administration endpoints. Everything
// else under BaseController is gated by RequireTenantContextFilter - this is the only way to
// opt out, so every use of it is a deliberate, reviewable exclusion.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipTenantContextCheckAttribute : Attribute
{
}
