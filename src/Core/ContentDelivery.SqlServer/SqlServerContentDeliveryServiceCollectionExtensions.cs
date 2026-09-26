using Cms.ContentDelivery.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cms.ContentDelivery;

public static class SqlServerContentDeliveryServiceCollectionExtensions
{
    // Registers IContentDeliveryClient over the CMS database: binds and validates the
    // ContentDelivery section (the website's single ApplicationId, see AddContentDelivery) and
    // reads with the CMS connection string the website supplies at startup. Use a read-only
    // database principal; the SDK never writes. Reads are metered ("Cms.ContentDelivery" meter)
    // and cached in-process only when ContentDelivery:CacheEnabled is true.
    public static IServiceCollection AddSqlServerContentDelivery(this IServiceCollection services, IConfiguration configuration, string cmsConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(cmsConnectionString);

        services.AddContentDelivery(configuration);
        services.AddDbContext<ContentDeliveryDbContext>(options => options.UseSqlServer(cmsConnectionString));
        services.AddContentDeliveryClient<SqlContentDeliveryClient>();

        return services;
    }

    // Opt-in health checks, tagged "content-delivery": "content-delivery-configuration" (the
    // ContentDelivery section is valid) and "content-delivery-database" (the CMS database accepts
    // a connection; reads no content). Maps no endpoint - the website decides where to expose them.
    public static IHealthChecksBuilder AddContentDeliveryHealthChecks(this IHealthChecksBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string[] tags = ["content-delivery"];
        return builder
            .AddCheck<ContentDeliveryConfigurationHealthCheck>("content-delivery-configuration", tags: tags)
            .AddCheck<ContentDeliveryDatabaseHealthCheck>("content-delivery-database", tags: tags);
    }
}
