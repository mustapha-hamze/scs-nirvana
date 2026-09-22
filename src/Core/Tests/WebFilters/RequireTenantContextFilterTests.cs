using System.Reflection;
using System.Security.Claims;
using Application.Contracts.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Web.Areas.BackOffice.Controllers;
using Web.Filters;
using Xunit;

namespace Core.Tests.WebFilters;

public class RequireTenantContextFilterTests
{
    private class FakeCurrentApplicationContext : ICurrentApplicationContext
    {
        public int? CurrentApplicationId { get; set; }
    }

    private static ActionExecutingContext CreateContext(bool authenticated, ActionDescriptor actionDescriptor = null, bool ajax = false)
    {
        var httpContext = new DefaultHttpContext();
        if (authenticated)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "user@example.com") }, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        if (ajax)
            httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor ?? new ActionDescriptor());

        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), controller: new object());
    }

    private static ActionExecutionDelegate Next(bool[] called) => () =>
    {
        called[0] = true;
        return Task.FromResult(new ActionExecutedContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            controller: new object()));
    };

    [Fact]
    public async Task ValidSelection_CallsNext()
    {
        var currentApplicationContext = new FakeCurrentApplicationContext { CurrentApplicationId = 5 };
        var guard = new Mock<ITenantAccessGuard>();
        guard.Setup(g => g.HasAccessAsync("user@example.com", 5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new RequireTenantContextFilter(currentApplicationContext, guard.Object);
        var context = CreateContext(authenticated: true);
        var called = new bool[1];

        await sut.OnActionExecutionAsync(context, Next(called));

        Assert.True(called[0]);
        Assert.Null(context.Result);
        Assert.Equal(5, currentApplicationContext.CurrentApplicationId);
    }

    [Fact]
    public async Task RevokedAccess_ClearsSessionAndRedirectsForNormalNavigation()
    {
        var currentApplicationContext = new FakeCurrentApplicationContext { CurrentApplicationId = 5 };
        var guard = new Mock<ITenantAccessGuard>();
        guard.Setup(g => g.HasAccessAsync("user@example.com", 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new RequireTenantContextFilter(currentApplicationContext, guard.Object);
        var context = CreateContext(authenticated: true, ajax: false);
        var called = new bool[1];

        await sut.OnActionExecutionAsync(context, Next(called));

        Assert.False(called[0]);
        Assert.Null(currentApplicationContext.CurrentApplicationId);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/BackOffice/Application/SelectApp", redirect.Url);
    }

    [Fact]
    public async Task RevokedAccess_ReturnsNotFoundForAjaxRequests()
    {
        var currentApplicationContext = new FakeCurrentApplicationContext { CurrentApplicationId = 5 };
        var guard = new Mock<ITenantAccessGuard>();
        guard.Setup(g => g.HasAccessAsync("user@example.com", 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new RequireTenantContextFilter(currentApplicationContext, guard.Object);
        var context = CreateContext(authenticated: true, ajax: true);
        var called = new bool[1];

        await sut.OnActionExecutionAsync(context, Next(called));

        Assert.False(called[0]);
        Assert.Null(currentApplicationContext.CurrentApplicationId);
        Assert.IsType<NotFoundResult>(context.Result);
    }

    [Fact]
    public async Task MissingSelection_DeniesWithoutCallingGuard()
    {
        var currentApplicationContext = new FakeCurrentApplicationContext();
        var guard = new Mock<ITenantAccessGuard>();

        var sut = new RequireTenantContextFilter(currentApplicationContext, guard.Object);
        var context = CreateContext(authenticated: true);
        var called = new bool[1];

        await sut.OnActionExecutionAsync(context, Next(called));

        Assert.False(called[0]);
        Assert.IsType<RedirectResult>(context.Result);
        guard.Verify(g => g.HasAccessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unauthenticated_SkipsCheckAndCallsNext()
    {
        var currentApplicationContext = new FakeCurrentApplicationContext();
        var guard = new Mock<ITenantAccessGuard>();

        var sut = new RequireTenantContextFilter(currentApplicationContext, guard.Object);
        var context = CreateContext(authenticated: false);
        var called = new bool[1];

        await sut.OnActionExecutionAsync(context, Next(called));

        Assert.True(called[0]);
        guard.Verify(g => g.HasAccessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SkipMarkedAction_BypassesCheckAndCallsNext()
    {
        var actionDescriptor = new ControllerActionDescriptor
        {
            MethodInfo = typeof(ApplicationController).GetMethod(nameof(ApplicationController.SelectApp)),
            ControllerTypeInfo = typeof(ApplicationController).GetTypeInfo(),
        };

        var currentApplicationContext = new FakeCurrentApplicationContext();
        var guard = new Mock<ITenantAccessGuard>();

        var sut = new RequireTenantContextFilter(currentApplicationContext, guard.Object);
        var context = CreateContext(authenticated: true, actionDescriptor: actionDescriptor);
        var called = new bool[1];

        await sut.OnActionExecutionAsync(context, Next(called));

        Assert.True(called[0]);
        guard.Verify(g => g.HasAccessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
