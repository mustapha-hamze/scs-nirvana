using System.Reflection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Web.Filters;

// Runs before every BackOffice tenant-scoped action (see BaseController). A session's selected
// application id is a selector, not a grant: this re-resolves the authenticated user and
// revalidates the selection against live membership/application state on every request, and
// denies outright when nothing is selected - a stale/unauthorized/missing selection all fail
// closed identically, without revealing which check failed.
public sealed class RequireTenantContextFilter : IAsyncActionFilter
{
    private const string AjaxRequestedWithHeader = "X-Requested-With";
    private const string AjaxRequestedWithValue = "XMLHttpRequest";

    private readonly ICurrentApplicationContext _currentApplicationContext;
    private readonly ITenantAccessGuard _tenantAccessGuard;

    public RequireTenantContextFilter(ICurrentApplicationContext currentApplicationContext, ITenantAccessGuard tenantAccessGuard)
    {
        _currentApplicationContext = currentApplicationContext;
        _tenantAccessGuard = tenantAccessGuard;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Unauthenticated requests are rejected by [Authorize] separately; AllowAnonymous/skip
        // actions (login, logout, application selection/root administration) never need a tenant.
        if (IsExempt(context.ActionDescriptor) || context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var selectedApplicationId = _currentApplicationContext.CurrentApplicationId;
        var isSuperAdmin = context.HttpContext.User.IsInRole(ApplicationRoles.SuperAdmin);
        var hasAccess = selectedApplicationId is int applicationId
            && await _tenantAccessGuard.HasAccessAsync(context.HttpContext.User.Identity.Name, applicationId, isSuperAdmin);

        if (!hasAccess)
        {
            _currentApplicationContext.CurrentApplicationId = null;
            context.Result = IsAjaxRequest(context.HttpContext.Request)
                ? new NotFoundResult()
                : new RedirectResult("/BackOffice/Application/SelectApp");
            return;
        }

        await next();
    }

    private static bool IsExempt(ActionDescriptor actionDescriptor) =>
        actionDescriptor is ControllerActionDescriptor controllerActionDescriptor
        && (controllerActionDescriptor.MethodInfo.GetCustomAttribute<SkipTenantContextCheckAttribute>(inherit: true) != null
            || controllerActionDescriptor.ControllerTypeInfo.GetCustomAttribute<SkipTenantContextCheckAttribute>(inherit: true) != null);

    private static bool IsAjaxRequest(HttpRequest request) =>
        request.Headers[AjaxRequestedWithHeader] == AjaxRequestedWithValue;
}
