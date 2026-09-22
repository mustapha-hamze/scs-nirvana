using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Web.Areas.BackOffice.Controllers;
using Web.Filters;
using Xunit;

namespace Core.Tests.Architecture;

// RequireTenantContextFilter (applied via BaseController) is the only thing standing between an
// authenticated BackOffice request and a stale/unauthorized tenant selection. The only sanctioned
// way to bypass it is [SkipTenantContextCheck], and every use of that attribute must be one of
// these known, reviewed exclusions (authentication, logout, application selection/root admin) -
// this test fails the moment a new bypass shows up anywhere else, intentional or not.
public class TenantContextGuardCoverageTests
{
    [Fact]
    public void BaseController_CarriesTheRequireTenantContextFilter()
    {
        var hasFilter = typeof(BaseController)
            .GetCustomAttributes<ServiceFilterAttribute>(inherit: false)
            .Any(a => a.ServiceType == typeof(RequireTenantContextFilter));

        Assert.True(hasFilter,
            "BaseController must carry [ServiceFilter(typeof(RequireTenantContextFilter))] - every BackOffice controller inherits it from there.");
    }

    [Fact]
    public void SkipTenantContextCheck_IsOnlyUsedByKnownExclusions()
    {
        var controllerTypes = typeof(BaseController).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseController).IsAssignableFrom(t))
            .ToList();

        var classLevelSkips = controllerTypes
            .Where(t => t.GetCustomAttribute<SkipTenantContextCheckAttribute>(inherit: false) != null)
            .Select(t => t.FullName)
            .ToList();

        var methodLevelSkips = controllerTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<SkipTenantContextCheckAttribute>(inherit: false) != null)
                .Select(m => $"{t.FullName}.{m.Name}"))
            .Distinct()
            .ToList();

        var expectedClassLevelSkips = new[]
        {
            typeof(ApplicationController).FullName,
        };

        var expectedMethodLevelSkips = new[]
        {
            $"{typeof(AccountController).FullName}.Login",
            $"{typeof(AccountController).FullName}.Logout",
        };

        AssertSameSet("class-level", expectedClassLevelSkips, classLevelSkips);
        AssertSameSet("method-level", expectedMethodLevelSkips, methodLevelSkips);
    }

    private static void AssertSameSet(string label, IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var expectedList = expected.ToList();
        var actualList = actual.ToList();

        var unexpected = actualList.Except(expectedList).ToList();
        var missing = expectedList.Except(actualList).ToList();

        Assert.True(unexpected.Count == 0, $"Unexpected {label} tenant-guard bypass(es): {string.Join(", ", unexpected)}");
        Assert.True(missing.Count == 0, $"Expected {label} tenant-guard bypass(es) missing: {string.Join(", ", missing)}");
    }
}
