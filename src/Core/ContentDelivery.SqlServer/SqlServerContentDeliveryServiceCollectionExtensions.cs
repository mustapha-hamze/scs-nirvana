using Cms.ContentDelivery.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.ContentDelivery;

public static class SqlServerContentDeliveryServiceCollectionExtensions
{
    // Registers IContentDeliveryClient over the CMS database: binds and validates the
    // ContentDelivery section (the website's single ApplicationId, see AddContentDelivery) and
    // reads with the CMS connection string the website supplies at startup. Use a read-only
    // database principal; the SDK never writes.
    public static IServiceCollection AddSqlServerContentDelivery(this IServiceCollection services, IConfiguration configuration, string cmsConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(cmsConnectionString);

        services.AddContentDelivery(configuration);
        services.AddDbContext<ContentDeliveryDbContext>(options => options.UseSqlServer(cmsConnectionString));
        services.AddScoped<IContentDeliveryClient, SqlContentDeliveryClient>();

        return services;
    }
}
