using System.Diagnostics.Metrics;

namespace Cms.ContentDelivery;

// Low-cardinality delivery metrics on the "Cms.ContentDelivery" meter. Tags are only operation,
// outcome, cache and resolution, from fixed value sets - never content, ids, tenants, cultures,
// fingerprints or error text. Recording never throws into a read.
internal sealed class ContentDeliveryMetrics
{
    public const string MeterName = "Cms.ContentDelivery";

    private readonly Histogram<double> _duration;
    private readonly Counter<long> _cache;
    private readonly Counter<long> _resolutions;
    private readonly Counter<long> _fallbacks;

    // Uses the host's IMeterFactory when registered (ASP.NET Core adds one).
    public ContentDeliveryMetrics(IMeterFactory? meterFactory = null)
    {
        var meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);
        _duration = meter.CreateHistogram<double>("cms.content_delivery.read.duration", "s", "Duration of delivery reads, by operation, outcome and cache state.");
        _cache = meter.CreateCounter<long>("cms.content_delivery.cache.requests", "{request}", "Cache lookups, by operation and hit/miss.");
        _resolutions = meter.CreateCounter<long>("cms.content_delivery.localization.resolutions", "{item}", "Delivered items, by operation and localization source.");
        _fallbacks = meter.CreateCounter<long>("cms.content_delivery.localization.fallbacks", "{item}", "Translations or legacy snapshots rejected at read time, by outcome.");
    }

    // outcome: found | not_found | invalid_culture | canceled | error. cache: hit | miss | disabled.
    public void Read(string operation, string outcome, string cache, TimeSpan elapsed, IEnumerable<ContentSummary> delivered)
    {
        try
        {
            _duration.Record(elapsed.TotalSeconds, new("operation", operation), new("outcome", outcome), new("cache", cache));
            if (cache != "disabled")
                _cache.Add(1, new KeyValuePair<string, object?>("operation", operation), new("cache", cache));
            foreach (var source in delivered.GroupBy(s => s.Localization.Source))
                _resolutions.Add(source.Count(), new KeyValuePair<string, object?>("operation", operation), new("resolution", Resolution(source.Key)));
        }
        catch
        {
            // Telemetry must not fail a read (e.g. a throwing MeterListener callback).
        }
    }

    // outcome: stale_translation | invalid_translation | invalid_legacy.
    public void Fallback(string outcome)
    {
        try
        {
            _fallbacks.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        }
        catch
        {
            // As above.
        }
    }

    private static string Resolution(LocalizationSource source) => source switch
    {
        LocalizationSource.Translation => "translation",
        LocalizationSource.LegacyFarsi => "legacy_farsi",
        _ => "source"
    };
}
