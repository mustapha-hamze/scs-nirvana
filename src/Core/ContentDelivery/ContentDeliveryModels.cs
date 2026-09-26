namespace Cms.ContentDelivery;

// Read-only delivery DTOs. Layout, ids, media, files, galleries and element types always come
// from the master graph; only text fields are localized. They deliberately carry no application
// id, raw translation JSON, provider, model, job or error data.
// Nullability is part of the contract: CMS text fields are optional (the CMS does not require
// them), so they are nullable; values the SDK always supplies are non-null and `required`.

public enum LocalizationSource
{
    // The source (master) text.
    Source,

    // A current, validated translation for the resolved culture.
    Translation,

    // The legacy Farsi snapshot, during migration only.
    LegacyFarsi
}

public sealed record LocalizationInfo
{
    // The application's culture key the text was resolved for, or null when source text was
    // served because the application has no such active culture.
    public string? Culture { get; init; }
    public LocalizationSource Source { get; init; }
}

// Changes whenever the delivered content or its resolved localization changes; usable as an ETag
// or cache-validation token. Tag is opaque - do not parse it.
public sealed record DeliveryVersion
{
    public required string Tag { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record MediaReference
{
    public int Id { get; init; }
    public required string FileName { get; init; }
    public int Size { get; init; }
}

public sealed record ContentSummary
{
    public int Id { get; init; }
    public int TypeId { get; init; }
    public string? Title { get; init; }
    public string? HeadLine { get; init; }
    public string? Abstract { get; init; }
    public DateTime PublishedAt { get; init; }
    public MediaReference? PrimaryImage { get; init; }
    public required LocalizationInfo Localization { get; init; }
    public required DeliveryVersion Version { get; init; }
}

public sealed record ContentDocument
{
    public required ContentSummary Summary { get; init; }
    public string? Description { get; init; }
    public ContentDocumentMetadata? Metadata { get; init; }

    // Ordered by priority, then id.
    public IReadOnlyList<ContentDocumentSection> Sections { get; init; } = [];
    public IReadOnlyList<MediaReference> Images { get; init; } = [];
    public IReadOnlyList<TaxonomyTerm> Categories { get; init; } = [];
    public IReadOnlyList<TaxonomyTerm> Tags { get; init; } = [];
}

public sealed record ContentDocumentMetadata
{
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Keywords { get; init; }
    public string? Description { get; init; }
}

public sealed record ContentDocumentSection
{
    public int Id { get; init; }
    public int Priority { get; init; }

    // Ordered by id.
    public IReadOnlyList<ContentDocumentElement> Elements { get; init; } = [];
}

public sealed record ContentDocumentElement
{
    public int Id { get; init; }
    public int ElementType { get; init; }
    public string? Title { get; init; }
    public string? TinyText { get; init; }
    public string? EditorText { get; init; }
    public string? FileName { get; init; }
    public string? GalleryImages { get; init; }
    public int Size { get; init; }
}

// A category (ParentId null for a root) or a tag (ParentId always null).
public sealed record TaxonomyTerm
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
}

public sealed record ContentTaxonomy
{
    public IReadOnlyList<TaxonomyTerm> Categories { get; init; } = [];
    public IReadOnlyList<TaxonomyTerm> Tags { get; init; } = [];
}

public sealed record SitemapEntry
{
    public int ContentId { get; init; }
    public int TypeId { get; init; }
    public DateTime LastModified { get; init; }

    // Culture keys the content can be served in, for alternate-language links.
    public IReadOnlyList<string> Cultures { get; init; } = [];
}
