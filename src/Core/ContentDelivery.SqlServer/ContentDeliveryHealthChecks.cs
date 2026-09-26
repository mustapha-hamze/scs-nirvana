using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Cms.ContentDelivery.SqlServer;

// Results carry fixed descriptions and no exception, so connection strings, server names and
// provider errors never reach a health report.
internal sealed class ContentDeliveryConfigurationHealthCheck(IOptions<ContentDeliveryOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = options.Value;
            return Task.FromResult(HealthCheckResult.Healthy("Content delivery configuration is valid."));
        }
        catch (OptionsValidationException e)
        {
            // Failure messages name the offending setting, never its value.
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus,
                $"Content delivery configuration is invalid: {string.Join(" ", e.Failures)}"));
        }
    }
}

internal sealed class ContentDeliveryDatabaseHealthCheck(ContentDeliveryDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        bool available;
        try
        {
            available = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            available = false;
        }

        return available
            ? HealthCheckResult.Healthy("Content delivery database is reachable.")
            : new HealthCheckResult(context.Registration.FailureStatus, "Content delivery database is unavailable.");
    }
}
