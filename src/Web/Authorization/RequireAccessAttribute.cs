using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Authorization;

// Tenant-scoped access-key gate. Must run strictly after RequireTenantContextFilter so a
// missing/stale/unauthorized selected-application request keeps its existing
// redirect/404-and-clear-session outcome (see RequireTenantContextFilter) instead of a premature
// 403 here - RequireTenantContextFilter is an Action filter with the default Order (0); this sets
// an explicit Order of 1 so it always runs after, regardless of controller/action filter scope.
// The application id is taken solely from ICurrentApplicationContext (never request input), and
// RequireApplicationId() is safe to call unconditionally here because that ordering guarantees
// the tenant guard already validated the selection.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequireAccessAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    private readonly string[] _anyOfKeys;

    public RequireAccessAttribute(params string[] anyOfKeys)
    {
        _anyOfKeys = anyOfKeys;
    }

    public int Order => 1;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authorizer = context.HttpContext.RequestServices.GetRequiredService<AccessKeyAuthorizer>();
        var applicationContext = context.HttpContext.RequestServices.GetRequiredService<ICurrentApplicationContext>();

        var hasAccess = await authorizer.HasAccessAsync(
            context.HttpContext.User, applicationContext.RequireApplicationId(), _anyOfKeys);

        if (!hasAccess)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await next();
    }
}
