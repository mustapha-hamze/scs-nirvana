using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cms.ContentDelivery;

// The website's "ContentDelivery" configuration section: the one application it delivers.
public sealed class ContentDeliveryOptions
{
    public const string SectionName = "ContentDelivery";

    public int ApplicationId { get; init; }

    // Migration-only: serve the legacy Farsi snapshot when no current translation exists for
    // LegacyFallbackCulture. Off by default; when on, LegacyFallbackCulture is required.
    public bool LegacyFallbackEnabled { get; init; }

    // The culture key (e.g. "fa-IR") the legacy snapshot is written in. Ignored when disabled.
    public string? LegacyFallbackCulture { get; init; }
}

internal static class CultureTag
{
    // A BCP 47-shaped tag, e.g. "fa", "fa-IR", "zh-Hant-TW". \z (not $) so a trailing newline is
    // rejected.
    private static readonly Regex Shape = new(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}\z", RegexOptions.CultureInvariant);

    public static bool IsWellFormed(string? culture) => culture != null && Shape.IsMatch(culture);
}

// Application id 0 (a missing section) or a negative id must never stand in for a tenant.
internal sealed class ContentDeliveryOptionsValidator : IValidateOptions<ContentDeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, ContentDeliveryOptions options)
    {
        if (options.ApplicationId < 1)
            return ValidateOptionsResult.Fail($"{ContentDeliveryOptions.SectionName}:ApplicationId must be a positive application id.");
        if (options.LegacyFallbackEnabled && !CultureTag.IsWellFormed(options.LegacyFallbackCulture))
            return ValidateOptionsResult.Fail($"{ContentDeliveryOptions.SectionName}:LegacyFallbackCulture must be a culture key when LegacyFallbackEnabled is true.");
        return ValidateOptionsResult.Success;
    }
}

// The tenant every delivery read is scoped to, plus the legacy fallback culture (null when the
// fallback is disabled). Captured once from the validated startup configuration; later
// configuration reloads cannot re-point it.
internal sealed class ContentDeliveryTenant
{
    public ContentDeliveryTenant(int applicationId, string? legacyFallbackCulture = null)
    {
        ApplicationId = applicationId >= 1
            ? applicationId
            : throw new ArgumentOutOfRangeException(nameof(applicationId), applicationId, "Application id must be positive.");
        LegacyFallbackCulture = legacyFallbackCulture == null || CultureTag.IsWellFormed(legacyFallbackCulture)
            ? legacyFallbackCulture
            : throw new ArgumentException("Legacy fallback culture must be a culture key.", nameof(legacyFallbackCulture));
    }

    public int ApplicationId { get; }

    public string? LegacyFallbackCulture { get; }
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
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ContentDeliveryOptions>>().Value;
            return new ContentDeliveryTenant(options.ApplicationId, options.LegacyFallbackEnabled ? options.LegacyFallbackCulture : null);
        });

        return services;
    }
}
