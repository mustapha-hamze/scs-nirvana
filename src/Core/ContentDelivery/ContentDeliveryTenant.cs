using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cms.ContentDelivery;

// The website's "ContentDelivery" configuration section: the one application it delivers.
public sealed class ContentDeliveryOptions
{
    public const string SectionName = "ContentDelivery";

    public int ApplicationId { get; init; }
}

// Application id 0 (a missing section) or a negative id must never stand in for a tenant.
internal sealed class ContentDeliveryOptionsValidator : IValidateOptions<ContentDeliveryOptions>
{
    public ValidateOptionsResult Validate(string name, ContentDeliveryOptions options) =>
        options.ApplicationId >= 1
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{ContentDeliveryOptions.SectionName}:ApplicationId must be a positive application id.");
}

// The tenant every delivery read is scoped to. Captured once from the validated startup
// configuration; later configuration reloads cannot re-point it.
internal sealed class ContentDeliveryTenant
{
    public ContentDeliveryTenant(int applicationId)
    {
        ApplicationId = applicationId >= 1
            ? applicationId
            : throw new ArgumentOutOfRangeException(nameof(applicationId), applicationId, "Application id must be positive.");
    }

    public int ApplicationId { get; }
}

public static class ContentDeliveryServiceCollectionExtensions
{
    // Binds and validates the ContentDelivery section at startup (ValidateOnStart). One website
    // delivers one application, so a second registration is rejected rather than merged.
    public static IServiceCollection AddContentDelivery(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (services.Any(d => d.ServiceType == typeof(ContentDeliveryTenant)))
            throw new InvalidOperationException("AddContentDelivery has already been called; a website delivers exactly one application.");

        services.AddOptions<ContentDeliveryOptions>()
            .Bind(configuration.GetSection(ContentDeliveryOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ContentDeliveryOptions>, ContentDeliveryOptionsValidator>();
        services.AddSingleton(sp => new ContentDeliveryTenant(sp.GetRequiredService<IOptions<ContentDeliveryOptions>>().Value.ApplicationId));

        return services;
    }
}
