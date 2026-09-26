using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Cms.ContentDelivery.SqlServer;

// IContentDeliveryClient over the CMS tables, scoped to the startup-bound application.
// Rules (docs/Content-Delivery-Discovery.md §4):
// - Content is delivered only when owned by the tenant, IsActive and !IsDeleted; sections,
//   elements, images, categories and tags only when IsActive and !IsDeleted. Metadata only when
//   !IsDeleted (its IsActive is ignored, as in every existing read). PublishDt is not a gate.
// - Children are reached only through tenant-visible content, and categories/tags additionally
//   require their own ApplicationId to be the tenant, so cross-tenant relation rows never leak.
// - Every order ends with Id. Listing: UpdatedDT desc, Id desc (pending the published-date
//   decision D6).
// - Source text only (Phase 2): a well-formed culture is accepted but master text is served.
internal sealed class SqlContentDeliveryClient : IContentDeliveryClient
{
    // C2: a BCP 47-shaped tag. \z (not $) so a trailing newline is rejected.
    private static readonly Regex CultureTag = new(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}\z", RegexOptions.CultureInvariant);

    private static readonly LocalizationInfo SourceText = new() { Source = LocalizationSource.Source };

    private readonly ContentDeliveryDbContext _db;
    private readonly int _applicationId;

    public SqlContentDeliveryClient(ContentDeliveryDbContext db, ContentDeliveryTenant tenant)
    {
        _db = db;
        _applicationId = tenant.ApplicationId;
    }

    private IQueryable<ContentRow> VisibleContents =>
        _db.Contents.Where(c => c.ApplicationId == _applicationId && c.IsActive && !c.IsDeleted);

    private IQueryable<CategoryRow> VisibleCategories =>
        _db.Categories.Where(t => t.ApplicationId == _applicationId && t.IsActive && !t.IsDeleted);

    private IQueryable<TagRow> VisibleTags =>
        _db.Tags.Where(t => t.ApplicationId == _applicationId && t.IsActive && !t.IsDeleted);

    public async Task<ContentDeliveryResult<ContentDocument>> GetDocumentAsync(int contentId, string? culture = null, CancellationToken cancellationToken = default)
    {
        if (!IsValidCulture(culture))
            return ContentDeliveryResult<ContentDocument>.InvalidCulture();
        if (contentId <= 0)
            return ContentDeliveryResult<ContentDocument>.NotFound();

        var documents = await LoadDocumentsAsync([contentId], cancellationToken);
        return documents.TryGetValue(contentId, out var document)
            ? ContentDeliveryResult<ContentDocument>.Found(document)
            : ContentDeliveryResult<ContentDocument>.NotFound();
    }

    public async Task<ContentDeliveryResult<IReadOnlyList<ContentDocument>>> GetDocumentSetAsync(IReadOnlyList<int> contentIds, string? culture = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);
        if (contentIds.Count > ContentDeliveryLimits.MaxDocumentSetSize)
            throw new ArgumentException($"At most {ContentDeliveryLimits.MaxDocumentSetSize} content ids can be requested at once.", nameof(contentIds));
        if (!IsValidCulture(culture))
            return ContentDeliveryResult<IReadOnlyList<ContentDocument>>.InvalidCulture();

        // Requested order; a duplicate keeps its first position.
        var ids = contentIds.Where(id => id > 0).Distinct().ToList();
        var documents = ids.Count == 0 ? new Dictionary<int, ContentDocument>() : await LoadDocumentsAsync(ids, cancellationToken);
        IReadOnlyList<ContentDocument> ordered = ids.Where(documents.ContainsKey).Select(id => documents[id]).ToList();
        return ContentDeliveryResult<IReadOnlyList<ContentDocument>>.Found(ordered);
    }

    public async Task<ContentDeliveryResult<ContentPage<ContentSummary>>> GetListingAsync(ContentListingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!IsValidCulture(query.Culture))
            return ContentDeliveryResult<ContentPage<ContentSummary>>.InvalidCulture();

        // A category/tag that is missing, hidden or another tenant's matches nothing (empty page),
        // as Core's listings do today; decision D5 may later turn this into NotFound.
        var contents = VisibleContents;
        if (query.TypeId is { } typeId)
            contents = contents.Where(c => c.TypeId == typeId);
        if (query.CategoryId is { } categoryId)
            contents = contents.Where(c => _db.ContentCategories.Any(r => r.ContentId == c.Id && r.CategoryId == categoryId
                && VisibleCategories.Any(t => t.Id == r.CategoryId)));
        if (query.TagId is { } tagId)
            contents = contents.Where(c => _db.ContentTags.Any(r => r.ContentId == c.Id && r.TagId == tagId
                && VisibleTags.Any(t => t.Id == r.TagId)));

        var total = await contents.CountAsync(cancellationToken);
        var skip = (long)(query.PageNumber - 1) * query.PageSize;
        var items = new List<ContentSummary>();

        if (skip < total)
        {
            var heads = await contents
                .OrderByDescending(c => c.UpdatedDT).ThenByDescending(c => c.Id)
                .Skip((int)skip).Take(query.PageSize)
                .Select(c => new Head(c.Id, c.TypeId, c.Title, c.HeadLine, c.Abstract, null, c.PublishDt, c.UpdatedDT))
                .ToListAsync(cancellationToken);
            var images = await LoadImagesAsync(heads.Select(h => h.Id).ToList(), cancellationToken);
            items.AddRange(heads.Select(h => ToSummary(h, images[h.Id].FirstOrDefault())));
        }

        return ContentDeliveryResult<ContentPage<ContentSummary>>.Found(new ContentPage<ContentSummary>
        {
            Items = items,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total
        });
    }

    public async Task<ContentTaxonomy> GetTaxonomyAsync(CancellationToken cancellationToken = default)
    {
        var categories = await VisibleCategories.OrderBy(t => t.Id)
            .Select(t => new TaxonomyTerm { Id = t.Id, ParentId = t.ParentId == 0 ? null : t.ParentId, Title = t.Title, Description = t.Description })
            .ToListAsync(cancellationToken);
        var tags = await VisibleTags.OrderBy(t => t.Id)
            .Select(t => new TaxonomyTerm { Id = t.Id, Title = t.Title })
            .ToListAsync(cancellationToken);

        return new ContentTaxonomy { Categories = categories, Tags = tags };
    }

    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default) =>
        // Cultures stays empty until Phase 3 resolves localized availability (decision D4).
        await VisibleContents.OrderBy(c => c.Id)
            .Select(c => new SitemapEntry { ContentId = c.Id, TypeId = c.TypeId, LastModified = c.UpdatedDT })
            .ToListAsync(cancellationToken);

    // null/empty = source. Phase 2 accepts any well-formed tag and still serves source text.
    private static bool IsValidCulture(string? culture) => string.IsNullOrEmpty(culture) || CultureTag.IsMatch(culture);

    // One query per graph level for the whole batch (no joins across collections, so no cartesian
    // explosion, and no per-document or per-section round trips). Children are only read for ids
    // the tenant-scoped content query returned.
    private async Task<Dictionary<int, ContentDocument>> LoadDocumentsAsync(IReadOnlyCollection<int> requestedIds, CancellationToken ct)
    {
        var heads = await VisibleContents.Where(c => requestedIds.Contains(c.Id))
            .Select(c => new Head(c.Id, c.TypeId, c.Title, c.HeadLine, c.Abstract, c.Description, c.PublishDt, c.UpdatedDT))
            .ToListAsync(ct);
        if (heads.Count == 0)
            return new();

        var ids = heads.Select(h => h.Id).ToList();

        var metadata = await _db.Metadata.Where(m => !m.IsDeleted && ids.Contains(m.ContentId))
            .OrderBy(m => m.Id)
            .Select(m => new { m.ContentId, Value = new ContentDocumentMetadata { Title = m.Title, Author = m.Author, Keywords = m.Keywords, Description = m.Description } })
            .ToListAsync(ct);

        var visibleSections = _db.Sections.Where(s => s.IsActive && !s.IsDeleted && ids.Contains(s.ContentId));
        var sections = await visibleSections.OrderBy(s => s.Priority).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.ContentId, s.Priority })
            .ToListAsync(ct);

        var elements = await _db.Elements
            .Where(e => e.IsActive && !e.IsDeleted && visibleSections.Any(s => s.Id == e.SectionId))
            .OrderBy(e => e.Id)
            .Select(e => new
            {
                e.SectionId,
                Value = new ContentDocumentElement
                {
                    Id = e.Id,
                    ElementType = e.ElementType,
                    Title = e.ElementTitle,
                    TinyText = e.TinyText,
                    EditorText = e.EditorText,
                    FileName = e.FileNameText,
                    GalleryImages = e.GalleryImages,
                    Size = e.Size
                }
            })
            .ToListAsync(ct);

        var imagesByContent = await LoadImagesAsync(ids, ct);

        var categories = await (
                from r in _db.ContentCategories
                join t in VisibleCategories on r.CategoryId equals t.Id
                where ids.Contains(r.ContentId)
                orderby t.Id
                select new { r.ContentId, Value = new TaxonomyTerm { Id = t.Id, ParentId = t.ParentId == 0 ? null : t.ParentId, Title = t.Title, Description = t.Description } })
            .ToListAsync(ct);

        var tags = await (
                from r in _db.ContentTags
                join t in VisibleTags on r.TagId equals t.Id
                where ids.Contains(r.ContentId)
                orderby t.Id
                select new { r.ContentId, Value = new TaxonomyTerm { Id = t.Id, Title = t.Title } })
            .ToListAsync(ct);

        var elementsBySection = elements.ToLookup(e => e.SectionId, e => e.Value);
        var sectionsByContent = sections.ToLookup(s => s.ContentId, s => new ContentDocumentSection
        {
            Id = s.Id,
            Priority = s.Priority,
            Elements = elementsBySection[s.Id].ToList()
        });
        var metadataByContent = metadata.ToLookup(m => m.ContentId, m => m.Value);
        var categoriesByContent = categories.ToLookup(c => c.ContentId, c => c.Value);
        var tagsByContent = tags.ToLookup(t => t.ContentId, t => t.Value);

        return heads.ToDictionary(h => h.Id, h =>
        {
            var contentImages = imagesByContent[h.Id].ToList();
            return new ContentDocument
            {
                Summary = ToSummary(h, contentImages.FirstOrDefault()),
                Description = h.Description,
                Metadata = metadataByContent[h.Id].FirstOrDefault(),
                Sections = sectionsByContent[h.Id].ToList(),
                Images = contentImages,
                Categories = categoriesByContent[h.Id].ToList(),
                Tags = tagsByContent[h.Id].ToList()
            };
        });
    }

    // Visible images with a usable file name, by content, lowest Id first. Callers pass only
    // tenant-visible content ids. The first entry is the summary's PrimaryImage.
    // ponytail: lowest-Id valid image of any size fills PrimaryImage until the contract defines a
    // size-variant rule (discovery gap G1).
    private async Task<ILookup<int, MediaReference>> LoadImagesAsync(IReadOnlyCollection<int> contentIds, CancellationToken ct)
    {
        var rows = await _db.Images
            .Where(i => i.IsActive && !i.IsDeleted && contentIds.Contains(i.ContentId) && i.ImageFileName != null && i.ImageFileName != "")
            .OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.ContentId, i.ImageFileName, i.Size })
            .ToListAsync(ct);

        // Whitespace is checked here, not in SQL: SQL Server's string comparison ignores only
        // trailing spaces, so tabs/newlines-only names would slip through a server-side check.
        return rows.Where(i => !string.IsNullOrWhiteSpace(i.ImageFileName))
            .ToLookup(i => i.ContentId, i => new MediaReference { Id = i.Id, FileName = i.ImageFileName!, Size = i.Size });
    }

    private static ContentSummary ToSummary(Head h, MediaReference? primaryImage) => new()
    {
        Id = h.Id,
        TypeId = h.TypeId,
        Title = h.Title,
        HeadLine = h.HeadLine,
        Abstract = h.Abstract,
        PublishedAt = h.PublishDt,
        PrimaryImage = primaryImage,
        Localization = SourceText,
        // ponytail: keyed on the content row's UpdatedDT only; a child edit that does not touch
        // the content row keeps the same tag. Phase 4 (caching) should version the whole graph.
        Version = new DeliveryVersion { Tag = $"src-{h.Id}-{h.UpdatedDT.Ticks:x}", UpdatedAt = h.UpdatedDT }
    };

    private sealed record Head(int Id, int TypeId, string? Title, string? HeadLine, string? Abstract, string? Description, DateTime PublishDt, DateTime UpdatedDT);
}
