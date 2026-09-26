using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Cms.ContentDelivery;

// Where the decorator keeps successful reads. Keys already carry tenant, operation, query shape
// and culture; values are frozen DTOs that callers cannot mutate.
internal interface IContentDeliveryCache
{
    bool TryGet(string key, out object? value);

    void Set(string key, object value);
}

// Process-local TTL cache. Entries are never refreshed or invalidated by writes: a read is served
// for at most the TTL, then re-read.
internal sealed class MemoryContentDeliveryCache(TimeProvider time, TimeSpan ttl) : IContentDeliveryCache
{
    // ponytail: fixed entry cap, and a full cache simply stops adding until entries expire; add
    // LRU eviction or a size-based limit if hit rates suffer.
    internal const int MaxEntries = 1024;

    private readonly ConcurrentDictionary<string, (object Value, DateTimeOffset Expires)> _entries = new(StringComparer.Ordinal);

    public bool TryGet(string key, out object? value)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.Expires > time.GetUtcNow())
            {
                value = entry.Value;
                return true;
            }
            _entries.TryRemove(new(key, entry));
        }
        value = null;
        return false;
    }

    public void Set(string key, object value)
    {
        var now = time.GetUtcNow();
        if (_entries.Count >= MaxEntries)
        {
            foreach (var entry in _entries)
                if (entry.Value.Expires <= now)
                    _entries.TryRemove(entry);
        }
        if (_entries.Count < MaxEntries || _entries.ContainsKey(key))
            _entries[key] = (value, now + ttl);
    }
}

// Wraps the adapter's client: records metrics for every read and, when a cache is given, serves
// successful results from it. NotFound, InvalidCulture and exceptions are never cached.
internal sealed class ContentDeliveryClientDecorator(IContentDeliveryClient inner, ContentDeliveryMetrics metrics, IContentDeliveryCache? cache, int applicationId)
    : IContentDeliveryClient
{
    public Task<ContentDeliveryResult<ContentDocument>> GetDocumentAsync(int contentId, string? culture = null, CancellationToken cancellationToken = default) =>
        ReadAsync("document", $"{contentId}|{culture}",
            () => inner.GetDocumentAsync(contentId, culture, cancellationToken),
            r => r.TryGetValue(out var d) ? ContentDeliveryResult<ContentDocument>.Found(Freeze(d)) : null,
            r => r.Status, r => r.Value is { } d ? [d.Summary] : []);

    public Task<ContentDeliveryResult<IReadOnlyList<ContentDocument>>> GetDocumentSetAsync(IReadOnlyList<int> contentIds, string? culture = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);
        return ReadAsync("document_set", $"{string.Join(',', contentIds)}|{culture}",
            () => inner.GetDocumentSetAsync(contentIds, culture, cancellationToken),
            r => r.TryGetValue(out var set) ? ContentDeliveryResult<IReadOnlyList<ContentDocument>>.Found(ReadOnly(set, Freeze)) : null,
            r => r.Status, r => r.Value?.Select(d => d.Summary) ?? []);
    }

    public Task<ContentDeliveryResult<ContentPage<ContentSummary>>> GetListingAsync(ContentListingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadAsync("listing", $"{query.TypeId}|{query.CategoryId}|{query.TagId}|{query.PageNumber}|{query.PageSize}|{query.Culture}",
            () => inner.GetListingAsync(query, cancellationToken),
            r => r.TryGetValue(out var page) ? ContentDeliveryResult<ContentPage<ContentSummary>>.Found(page with { Items = ReadOnly(page.Items) }) : null,
            r => r.Status, r => r.Value?.Items ?? []);
    }

    public Task<ContentTaxonomy> GetTaxonomyAsync(CancellationToken cancellationToken = default) =>
        ReadAsync("taxonomy", "",
            () => inner.GetTaxonomyAsync(cancellationToken),
            t => t with { Categories = ReadOnly(t.Categories), Tags = ReadOnly(t.Tags) },
            _ => ContentDeliveryStatus.Found, _ => []);

    public Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default) =>
        ReadAsync("sitemap", "",
            () => inner.GetSitemapEntriesAsync(cancellationToken),
            entries => ReadOnly(entries, e => e with { Cultures = ReadOnly(e.Cultures) }),
            _ => ContentDeliveryStatus.Found, _ => []);

    // shape must end with the (unvalidated, free-text) culture so it cannot collide with the
    // fixed-count fields before it. cacheable returns the frozen value to cache, or null.
    private async Task<T> ReadAsync<T>(string operation, string shape, Func<Task<T>> read, Func<T, T?> cacheable,
        Func<T, ContentDeliveryStatus> status, Func<T, IEnumerable<ContentSummary>> summaries) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        var key = $"{applicationId}|{operation}|{shape}";
        var cacheState = cache == null ? "disabled" : "miss";
        try
        {
            T result;
            if (cache != null && cache.TryGet(key, out var hit))
            {
                cacheState = "hit";
                result = (T)hit!;
            }
            else
            {
                result = await read();
                if (cache != null && cacheable(result) is { } frozen)
                {
                    cache.Set(key, frozen);
                    result = frozen;
                }
            }

            metrics.Read(operation, Outcome(status(result)), cacheState, Stopwatch.GetElapsedTime(started), summaries(result));
            return result;
        }
        catch (Exception e)
        {
            metrics.Read(operation, e is OperationCanceledException ? "canceled" : "error", cacheState, Stopwatch.GetElapsedTime(started), []);
            throw;
        }
    }

    private static string Outcome(ContentDeliveryStatus status) => status switch
    {
        ContentDeliveryStatus.Found => "found",
        ContentDeliveryStatus.NotFound => "not_found",
        _ => "invalid_culture"
    };

    // Records are init-only; lists are the only mutable part (a caller could cast a List<T> back).
    private static ContentDocument Freeze(ContentDocument d) => d with
    {
        Sections = ReadOnly(d.Sections, s => s with { Elements = ReadOnly(s.Elements) }),
        Images = ReadOnly(d.Images),
        Categories = ReadOnly(d.Categories),
        Tags = ReadOnly(d.Tags)
    };

    private static ReadOnlyCollection<T> ReadOnly<T>(IReadOnlyList<T> items, Func<T, T>? freeze = null) =>
        (freeze == null ? items.ToList() : items.Select(freeze).ToList()).AsReadOnly();
}
