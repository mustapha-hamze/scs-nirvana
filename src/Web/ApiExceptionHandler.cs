using Microsoft.AspNetCore.Diagnostics;

namespace Web;

// Tried first by ExceptionHandlerMiddleware (registered via AddExceptionHandler<T>()) for every
// unhandled exception in the production pipeline. Logs server-side regardless of route, then only
// claims /api/... requests by writing an RFC 7807 body; every other path returns false so the
// middleware falls through to its configured ExceptionHandlingPath ("/Home/Error") re-execution.
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // By the time IExceptionHandler runs, ExceptionHandlerMiddleware has already rewritten
        // Request.Path to the configured ExceptionHandlingPath ("/Home/Error"); the pre-rewrite
        // path only survives on this feature.
        var originalPath = httpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path ?? httpContext.Request.Path.Value ?? string.Empty;

        _logger.LogError(exception, "Unhandled exception on {Method} {Path}. TraceId: {TraceId}",
            httpContext.Request.Method, originalPath, httpContext.TraceIdentifier);

        if (!originalPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            }
        });
    }
}
