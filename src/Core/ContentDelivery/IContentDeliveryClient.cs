using System.Diagnostics.CodeAnalysis;

namespace Cms.ContentDelivery;

// Read-only delivery of the website's CMS content. Every operation is scoped to the single
// application bound at startup (AddContentDelivery); no operation accepts an application id, and
// ids passed here are lookup inputs only - content owned by another application is NotFound.
// culture is a language tag such as "fa-IR"; null or empty reads the source (master) text.
// Nothing here writes, queues translation work or calls a translation provider.
public interface IContentDeliveryClient
{
    // One complete document (sections, elements, media, taxonomy) - no follow-up reads needed.
    Task<ContentDeliveryResult<ContentDocument>> GetDocumentAsync(int contentId, string? culture = null, CancellationToken cancellationToken = default);

    // Several documents for one page composition, in the requested order. Ids that are not found
    // are omitted rather than failing the set. At most ContentDeliveryLimits.MaxDocumentSetSize ids.
    Task<ContentDeliveryResult<IReadOnlyList<ContentDocument>>> GetDocumentSetAsync(IReadOnlyList<int> contentIds, string? culture = null, CancellationToken cancellationToken = default);

    // A page of summaries filtered by type, category and/or tag.
    Task<ContentDeliveryResult<ContentPage<ContentSummary>>> GetListingAsync(ContentListingQuery query, CancellationToken cancellationToken = default);

    // The application's categories (a tree through ParentId) and tags, for menus and filters.
    Task<ContentTaxonomy> GetTaxonomyAsync(CancellationToken cancellationToken = default);

    // Lightweight entries for every publicly visible content item, for sitemap generation.
    Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default);
}

public static class ContentDeliveryLimits
{
    public const int MaxPageSize = 100;
    public const int MaxDocumentSetSize = 50;
}

public enum ContentDeliveryStatus
{
    Found,
    NotFound,

    // culture is not a well-formed language tag.
    InvalidCulture
}

// Value is set only when Status is Found. Check IsFound (or TryGetValue) before reading Value;
// the compiler then treats Value as non-null.
public sealed record ContentDeliveryResult<T> where T : notnull
{
    private ContentDeliveryResult(ContentDeliveryStatus status, T? value)
    {
        Status = status;
        Value = value;
    }

    public ContentDeliveryStatus Status { get; }
    public T? Value { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsFound => Status == ContentDeliveryStatus.Found;

    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = Value;
        return IsFound;
    }

    public static ContentDeliveryResult<T> Found(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ContentDeliveryStatus.Found, value);
    }

    public static ContentDeliveryResult<T> NotFound() => new(ContentDeliveryStatus.NotFound, default);

    public static ContentDeliveryResult<T> InvalidCulture() => new(ContentDeliveryStatus.InvalidCulture, default);
}

// Filters combine with AND; a null filter does not restrict. PageNumber is 1-based.
public sealed record ContentListingQuery
{
    private readonly int _pageNumber = 1;
    private readonly int _pageSize = 20;

    public int? TypeId { get; init; }
    public int? CategoryId { get; init; }
    public int? TagId { get; init; }
    public string? Culture { get; init; }

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(PageNumber), value, "PageNumber must be at least 1.");
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value is >= 1 and <= ContentDeliveryLimits.MaxPageSize
            ? value
            : throw new ArgumentOutOfRangeException(nameof(PageSize), value, $"PageSize must be between 1 and {ContentDeliveryLimits.MaxPageSize}.");
    }
}

public sealed record ContentPage<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
