using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    // In-process caching of delivery reads. Off by default. There is no write-side invalidation:
    // a cached read can be up to CacheTtl old, so keep it short.
    public bool CacheEnabled { get; init; }

    // How long a cached read is served, between 1 second and MaxCacheTtl.
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan MaxCacheTtl = TimeSpan.FromMinutes(5);
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
        if (options.CacheTtl < TimeSpan.FromSeconds(1) || options.CacheTtl > ContentDeliveryOptions.MaxCacheTtl)
            return ValidateOptionsResult.Fail($"{ContentDeliveryOptions.SectionName}:CacheTtl must be between 00:00:01 and {ContentDeliveryOptions.MaxCacheTtl}.");
        return ValidateOptionsResult.Success;
    }
}

// The tenant every delivery read is scoped to, plus the legacy fallback culture and cache TTL
// (each null when disabled). Captured once from the validated startup configuration; later
// configuration reloads cannot re-point it.
internal sealed class ContentDeliveryTenant
{
    public ContentDeliveryTenant(int applicationId, string? legacyFallbackCulture = null, TimeSpan? cacheTtl = null)
    {
        ApplicationId = applicationId >= 1
            ? applicationId
            : throw new ArgumentOutOfRangeException(nameof(applicationId), applicationId, "Application id must be positive.");
        LegacyFallbackCulture = legacyFallbackCulture == null || CultureTag.IsWellFormed(legacyFallbackCulture)
            ? legacyFallbackCulture
            : throw new ArgumentException("Legacy fallback culture must be a culture key.", nameof(legacyFallbackCulture));
        CacheTtl = cacheTtl is not { } ttl || (ttl > TimeSpan.Zero && ttl <= ContentDeliveryOptions.MaxCacheTtl)
            ? cacheTtl
            : throw new ArgumentOutOfRangeException(nameof(cacheTtl), cacheTtl, "Cache TTL is out of range.");
    }

    public int ApplicationId { get; }

    public string? LegacyFallbackCulture { get; }

    public TimeSpan? CacheTtl { get; }
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
            return new ContentDeliveryTenant(options.ApplicationId, options.LegacyFallbackEnabled ? options.LegacyFallbackCulture : null,
                options.CacheEnabled ? options.CacheTtl : null);
        });

        return services;
    }

    // Registers an adapter's client behind IContentDeliveryClient, decorated with metrics and,
    // when CacheEnabled, the in-process read cache. Call after AddContentDelivery.
    internal static IServiceCollection AddContentDeliveryClient<TClient>(this IServiceCollection services)
        where TClient : class, IContentDeliveryClient
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ContentDeliveryMetrics>();
        services.TryAddSingleton<IContentDeliveryCache>(sp => new MemoryContentDeliveryCache(
            sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ContentDeliveryTenant>().CacheTtl ?? throw new InvalidOperationException("Content delivery caching is disabled.")));
        services.AddScoped<TClient>();
        services.AddScoped<IContentDeliveryClient>(sp =>
        {
            var tenant = sp.GetRequiredService<ContentDeliveryTenant>();
            return new ContentDeliveryClientDecorator(sp.GetRequiredService<TClient>(), sp.GetRequiredService<ContentDeliveryMetrics>(),
                tenant.CacheTtl == null ? null : sp.GetRequiredService<IContentDeliveryCache>(), tenant.ApplicationId);
        });

        return services;
    }
}
