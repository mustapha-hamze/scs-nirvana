using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Core.Tests.TestSupport;

// Behavioral proof that a controller action's declared [Authorize(Roles = "...")] attributes
// actually deny/allow the way ASP.NET Core's authorization middleware would - not just that the
// attribute is present. Uses the framework's own RolesAuthorizationRequirement (both the
// requirement and its own handler), the same check the middleware runs before the action body.
public static class AuthorizeRoleTestHelper
{
    public static ClaimsPrincipal OrdinaryMember() => CreatePrincipal();

    public static ClaimsPrincipal SuperAdmin() => CreatePrincipal("SuperAdmin");

    private static ClaimsPrincipal CreatePrincipal(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    // True only if `principal` satisfies every role-restricted [Authorize] attribute declared
    // directly on `method`. Methods with no role-restricted [Authorize] attribute always satisfy
    // this (there is nothing role-based to deny on) - pair with a reflection check elsewhere if
    // "no role restriction at all" itself needs asserting.
    public static async Task<bool> SatisfiesAsync(MethodInfo method, ClaimsPrincipal principal)
    {
        var roleAttributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Where(a => !string.IsNullOrEmpty(a.Roles));

        foreach (var attribute in roleAttributes)
        {
            var requirement = new RolesAuthorizationRequirement(attribute.Roles.Split(','));
            var context = new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);

            await ((IAuthorizationHandler)requirement).HandleAsync(context);

            if (!context.HasSucceeded)
                return false;
        }

        return true;
    }
}
