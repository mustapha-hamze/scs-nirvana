using Cms.ContentLocalization;
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
// Localization (Phase 3): a culture must be an active, non-deleted global culture
// (ApplicationId 0), matched ignoring case. Text then resolves to a Ready, non-deleted translation
// for that culture whose fingerprint equals the current source's and whose payload exactly fits
// the master tree; else, for the configured legacy culture only, a valid legacy FarsiContent
// snapshot; else source text. Only text fields are localized; everything else is the master's.
// The fingerprint and parsers are the ones the translation workflow uses (ContentLocalization).
internal sealed class SqlContentDeliveryClient : IContentDeliveryClient
{
    private static readonly LocalizationInfo SourceRead = new() { Source = LocalizationSource.Source };

    private readonly ContentDeliveryDbContext _db;
    private readonly int _applicationId;
    private readonly string? _legacyCulture;

    public SqlContentDeliveryClient(ContentDeliveryDbContext db, ContentDeliveryTenant tenant)
    {
        _db = db;
        _applicationId = tenant.ApplicationId;
        _legacyCulture = tenant.LegacyFallbackCulture;
    }

    private IQueryable<ContentRow> VisibleContents =>
        _db.Contents.Where(c => c.ApplicationId == _applicationId && c.IsActive && !c.IsDeleted);

    private IQueryable<CategoryRow> VisibleCategories =>
        _db.Categories.Where(t => t.ApplicationId == _applicationId && t.IsActive && !t.IsDeleted);

    private IQueryable<TagRow> VisibleTags =>
        _db.Tags.Where(t => t.ApplicationId == _applicationId && t.IsActive && !t.IsDeleted);

    // Cultures are global rows today (docs/Content-Delivery-SDK-Implementation-Plan.md §6).
    private IQueryable<CultureRow> ActiveCultures =>
        _db.Cultures.Where(c => c.ApplicationId == 0 && c.IsActive && !c.IsDeleted && c.Key != null);

    public async Task<ContentDeliveryResult<ContentDocument>> GetDocumentAsync(int contentId, string? culture = null, CancellationToken cancellationToken = default)
    {
        var (valid, resolved) = await ResolveCultureAsync(culture, cancellationToken);
        if (!valid)
            return ContentDeliveryResult<ContentDocument>.InvalidCulture();
        if (contentId <= 0)
            return ContentDeliveryResult<ContentDocument>.NotFound();

        var documents = await LoadDocumentsAsync([contentId], resolved, cancellationToken);
        return documents.TryGetValue(contentId, out var document)
            ? ContentDeliveryResult<ContentDocument>.Found(document)
            : ContentDeliveryResult<ContentDocument>.NotFound();
    }

    public async Task<ContentDeliveryResult<IReadOnlyList<ContentDocument>>> GetDocumentSetAsync(IReadOnlyList<int> contentIds, string? culture = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);
        if (contentIds.Count > ContentDeliveryLimits.MaxDocumentSetSize)
            throw new ArgumentException($"At most {ContentDeliveryLimits.MaxDocumentSetSize} content ids can be requested at once.", nameof(contentIds));
        var (valid, resolved) = await ResolveCultureAsync(culture, cancellationToken);
        if (!valid)
            return ContentDeliveryResult<IReadOnlyList<ContentDocument>>.InvalidCulture();

        // Requested order; a duplicate keeps its first position.
        var ids = contentIds.Where(id => id > 0).Distinct().ToList();
        var documents = ids.Count == 0 ? new Dictionary<int, ContentDocument>() : await LoadDocumentsAsync(ids, resolved, cancellationToken);
        IReadOnlyList<ContentDocument> ordered = ids.Where(documents.ContainsKey).Select(id => documents[id]).ToList();
        return ContentDeliveryResult<IReadOnlyList<ContentDocument>>.Found(ordered);
    }

    public async Task<ContentDeliveryResult<ContentPage<ContentSummary>>> GetListingAsync(ContentListingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (valid, resolved) = await ResolveCultureAsync(query.Culture, cancellationToken);
        if (!valid)
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
            var heads = await SelectHeads(contents
                    .OrderByDescending(c => c.UpdatedDT).ThenByDescending(c => c.Id)
                    .Skip((int)skip).Take(query.PageSize))
                .ToListAsync(cancellationToken);
            var ids = heads.Select(h => h.Id).ToList();
            var images = await LoadImagesAsync(ids, cancellationToken);

            // Graphs only for items that have something to resolve against.
            var localized = new Dictionary<int, Localized>();
            if (resolved != null)
            {
                var candidates = await LoadCandidatesAsync(VisibleContents.Where(c => ids.Contains(c.Id)), [resolved], cancellationToken);
                var graphs = await LoadGraphsAsync(heads.Where(h => candidates.Has(h.Id)).ToList(), cancellationToken);
                foreach (var graph in graphs.Values)
                    localized[graph.Head.Id] = Resolve(graph, resolved, candidates);
            }

            items.AddRange(heads.Select(h => ToSummary(h, images[h.Id].FirstOrDefault(), localized.GetValueOrDefault(h.Id) ?? SourceText(h, resolved))));
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

    // D4: Cultures lists only the active global cultures a visible content item has a current
    // translation (or eligible legacy snapshot) in - never source-fallback-only cultures.
    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
    {
        var entries = await VisibleContents.OrderBy(c => c.Id)
            .Select(c => new SitemapEntry { ContentId = c.Id, TypeId = c.TypeId, LastModified = c.UpdatedDT })
            .ToListAsync(cancellationToken);

        var cultures = (await ActiveCultures.Select(c => new CultureEntry(c.Id, c.Key!)).ToListAsync(cancellationToken))
            .OrderBy(c => c.Key, StringComparer.Ordinal).ThenBy(c => c.Id)
            .ToList();
        if (entries.Count == 0 || cultures.Count == 0)
            return entries;

        var candidates = await LoadCandidatesAsync(VisibleContents, cultures, cancellationToken);
        var candidateIds = candidates.ContentIds.ToList();
        if (candidateIds.Count == 0)
            return entries;

        // ponytail: every candidate graph is held in memory at once; page through candidateIds if
        // a tenant's translated content outgrows that.
        var heads = await SelectHeads(VisibleContents.Where(c => candidateIds.Contains(c.Id))).ToListAsync(cancellationToken);
        var graphs = await LoadGraphsAsync(heads, cancellationToken);

        return entries.Select(e => graphs.TryGetValue(e.ContentId, out var graph)
                ? e with
                {
                    Cultures = cultures.Where(c => Resolve(graph, c, candidates).Info.Source != LocalizationSource.Source)
                        .Select(c => c.Key).Distinct().ToList()
                }
                : e)
            .ToList();
    }

    // Valid: null/empty (a source read, Culture null) or exactly one active global culture whose
    // key equals culture ignoring case. A malformed tag is rejected before any query.
    private async Task<(bool Valid, CultureEntry? Culture)> ResolveCultureAsync(string? culture, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(culture))
            return (true, null);
        if (!CultureTag.IsWellFormed(culture))
            return (false, null);

        // ToLower on both sides: case-insensitive on every provider, not only CI collations.
        var key = culture.ToLowerInvariant();
        var matches = await ActiveCultures.Where(c => c.Key!.ToLower() == key)
            .Select(c => new CultureEntry(c.Id, c.Key!))
            .Take(2)
            .ToListAsync(ct);
        return matches.Count == 1 ? (true, matches[0]) : (false, null);
    }

    private static IQueryable<Head> SelectHeads(IQueryable<ContentRow> contents) =>
        contents.Select(c => new Head(c.Id, c.TypeId, c.Title, c.HeadLine, c.Abstract, c.Description, c.PublishDt, c.UpdatedDT));

    // One query per graph level for the whole batch (no joins across collections, so no cartesian
    // explosion, and no per-document or per-section round trips). Children are only read for ids
    // the tenant-scoped content query returned.
    private async Task<Dictionary<int, ContentDocument>> LoadDocumentsAsync(IReadOnlyCollection<int> requestedIds, CultureEntry? culture, CancellationToken ct)
    {
        var heads = await SelectHeads(VisibleContents.Where(c => requestedIds.Contains(c.Id))).ToListAsync(ct);
        if (heads.Count == 0)
            return new();

        var ids = heads.Select(h => h.Id).ToList();
        var graphs = await LoadGraphsAsync(heads, ct);
        var candidates = culture == null ? null : await LoadCandidatesAsync(VisibleContents.Where(c => ids.Contains(c.Id)), [culture], ct);

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

        var categoriesByContent = categories.ToLookup(c => c.ContentId, c => c.Value);
        var tagsByContent = tags.ToLookup(t => t.ContentId, t => t.Value);

        return graphs.Values.ToDictionary(g => g.Head.Id, g =>
        {
            var h = g.Head;
            var localized = candidates != null && candidates.Has(h.Id) ? Resolve(g, culture!, candidates) : SourceText(h, culture);
            var text = localized.Text;
            var contentImages = imagesByContent[h.Id].ToList();
            return new ContentDocument
            {
                Summary = ToSummary(h, contentImages.FirstOrDefault(), localized),
                Description = text == null ? h.Description : text.Description,
                Metadata = g.Metadata is not { } m ? null
                    : text?.Metadata is { } t && t.Id == m.Id
                        ? new ContentDocumentMetadata { Title = t.Title, Author = t.Author, Keywords = t.Keywords, Description = t.Description }
                        : new ContentDocumentMetadata { Title = m.Title, Author = m.Author, Keywords = m.Keywords, Description = m.Description },
                Sections = ToSections(g, text),
                Images = contentImages,
                Categories = categoriesByContent[h.Id].ToList(),
                Tags = tagsByContent[h.Id].ToList()
            };
        });
    }

    // Visible sections/elements in delivery order, with text overlaid where the payload has the
    // same (section, element) node; nodes the text lacks keep the master's text.
    private static List<ContentDocumentSection> ToSections(Graph g, LocalizedText? text)
    {
        var elementTexts = (text?.Sections ?? []).SelectMany(s => s.Elements.Select(e => (Key: (s.Id, e.Id), Text: e)))
            .ToDictionary(x => x.Key, x => x.Text);

        return g.Sections.Where(s => s.IsActive).Select(s => new ContentDocumentSection
        {
            Id = s.Id,
            Priority = s.Priority,
            Elements = g.Elements[s.Id].Where(e => e.IsActive).Select(e =>
            {
                var translated = elementTexts.GetValueOrDefault((s.Id, e.Id));
                return new ContentDocumentElement
                {
                    Id = e.Id,
                    ElementType = e.ElementType,
                    Title = e.ElementTitle,
                    TinyText = translated == null ? e.TinyText : translated.TinyText,
                    EditorText = translated == null ? e.EditorText : translated.EditorText,
                    FileName = e.FileNameText,
                    GalleryImages = e.GalleryImages,
                    Size = e.Size
                };
            }).ToList()
        }).ToList();
    }

    // The non-deleted master graph - inactive sections and elements included, exactly what the
    // translation workflow fingerprints. Delivery shows only the active part.
    private async Task<Dictionary<int, Graph>> LoadGraphsAsync(IReadOnlyCollection<Head> heads, CancellationToken ct)
    {
        if (heads.Count == 0)
            return new();
        var ids = heads.Select(h => h.Id).ToList();

        var metadata = await _db.Metadata.Where(m => !m.IsDeleted && ids.Contains(m.ContentId))
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        var sections = _db.Sections.Where(s => !s.IsDeleted && ids.Contains(s.ContentId));
        var sectionRows = await sections.OrderBy(s => s.Priority).ThenBy(s => s.Id).ToListAsync(ct);
        var elementRows = await _db.Elements
            .Where(e => !e.IsDeleted && sections.Any(s => s.Id == e.SectionId))
            .OrderBy(e => e.Id)
            .ToListAsync(ct);

        var metadataByContent = metadata.ToLookup(m => m.ContentId);
        var sectionsByContent = sectionRows.ToLookup(s => s.ContentId);
        var elementsBySection = elementRows.ToLookup(e => e.SectionId);
        return heads.ToDictionary(h => h.Id, h => new Graph(h, metadataByContent[h.Id].FirstOrDefault(), sectionsByContent[h.Id].ToList(), elementsBySection));
    }

    // Ready, non-deleted translation rows and (for the legacy culture) legacy snapshots of the
    // given contents; only these are worth loading a graph for. Nothing is validated yet.
    private async Task<Candidates> LoadCandidatesAsync(IQueryable<ContentRow> contents, IReadOnlyList<CultureEntry> cultures, CancellationToken ct)
    {
        var cultureIds = cultures.Select(c => c.Id).ToList();
        var translations = await _db.Translations
            .Where(t => cultureIds.Contains(t.CultureId) && !t.IsDeleted && t.TranslationStatus == SourceFingerprint.ReadyStatus
                        && contents.Any(c => c.Id == t.ContentId))
            .Select(t => new { t.ContentId, t.CultureId, Value = new TranslationCandidate(t.SourceFingerprint, t.LocalizedTextJson, t.UpdatedDT) })
            .ToListAsync(ct);

        var legacy = cultures.Any(IsLegacyCulture)
            ? await contents.Where(c => c.FarsiContent != null && c.FarsiContent != "")
                .Select(c => new { c.Id, c.FarsiContent })
                .ToDictionaryAsync(c => c.Id, c => c.FarsiContent!, ct)
            : new Dictionary<int, string>();

        return new Candidates(translations.ToDictionary(t => (t.ContentId, t.CultureId), t => t.Value), legacy);
    }

    private bool IsLegacyCulture(CultureEntry culture) =>
        _legacyCulture != null && string.Equals(culture.Key, _legacyCulture, StringComparison.OrdinalIgnoreCase);

    private Localized Resolve(Graph graph, CultureEntry culture, Candidates candidates)
    {
        var h = graph.Head;
        if (candidates.Translations.TryGetValue((h.Id, culture.Id), out var translation)
            && translation.SourceFingerprint == graph.Fingerprint
            && LocalizedTextParser.ReadCurrent(translation.LocalizedTextJson, graph.Source) is { } text)
            return new(text, new() { Culture = culture.Key, Source = LocalizationSource.Translation },
                $"tr-{culture.Key}-{h.Id}-{h.UpdatedDT.Ticks:x}-{translation.UpdatedDT.Ticks:x}");

        if (IsLegacyCulture(culture)
            && candidates.Legacy.TryGetValue(h.Id, out var snapshot)
            && !string.IsNullOrWhiteSpace(snapshot)
            && LocalizedTextParser.ExtractLegacy(snapshot, graph.Source).Text is { } legacy)
            return new(legacy, new() { Culture = culture.Key, Source = LocalizationSource.LegacyFarsi },
                $"lf-{culture.Key}-{h.Id}-{h.UpdatedDT.Ticks:x}");

        return SourceText(h, culture);
    }

    private static Localized SourceText(Head h, CultureEntry? culture) =>
        new(null, culture == null ? SourceRead : new() { Culture = culture.Key, Source = LocalizationSource.Source },
            $"src-{h.Id}-{h.UpdatedDT.Ticks:x}");

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

    private static ContentSummary ToSummary(Head h, MediaReference? primaryImage, Localized localized) => new()
    {
        Id = h.Id,
        TypeId = h.TypeId,
        Title = localized.Text == null ? h.Title : localized.Text.Title,
        HeadLine = localized.Text == null ? h.HeadLine : localized.Text.HeadLine,
        Abstract = localized.Text == null ? h.Abstract : localized.Text.Abstract,
        PublishedAt = h.PublishDt,
        PrimaryImage = primaryImage,
        Localization = localized.Info,
        // ponytail: keyed on the content row's UpdatedDT (plus culture and translation row);
        // a child edit that does not touch the content row keeps the same source tag. Phase 4
        // (caching) should version the whole graph.
        Version = new DeliveryVersion { Tag = localized.Tag, UpdatedAt = h.UpdatedDT }
    };

    private sealed record Head(int Id, int TypeId, string? Title, string? HeadLine, string? Abstract, string? Description, DateTime PublishDt, DateTime UpdatedDT);

    private sealed record CultureEntry(int Id, string Key);

    private sealed record TranslationCandidate(string? SourceFingerprint, string? LocalizedTextJson, DateTime UpdatedDT);

    private sealed record Candidates(Dictionary<(int ContentId, int CultureId), TranslationCandidate> Translations, Dictionary<int, string> Legacy)
    {
        public IEnumerable<int> ContentIds => Translations.Keys.Select(k => k.ContentId).Concat(Legacy.Keys).Distinct();

        public bool Has(int contentId) => Legacy.ContainsKey(contentId) || Translations.Keys.Any(k => k.ContentId == contentId);
    }

    // Text null = source text. Tag is the summary's DeliveryVersion tag.
    private sealed record Localized(LocalizedText? Text, LocalizationInfo Info, string Tag);

    // A content's non-deleted master graph; sections by Priority then Id, elements by Id.
    private sealed class Graph(Head head, MetadataRow? metadata, IReadOnlyList<SectionRow> sections, ILookup<int, ElementRow> elements)
    {
        private SourceContent? _source;
        private string? _fingerprint;

        public Head Head => head;
        public MetadataRow? Metadata => metadata;
        public IReadOnlyList<SectionRow> Sections => sections;
        public ILookup<int, ElementRow> Elements => elements;

        public SourceContent Source => _source ??= new SourceContent(head.Id, head.TypeId, head.Title, head.HeadLine, head.Abstract, head.Description,
            metadata == null ? null : new SourceMetadata(metadata.Id, metadata.Title, metadata.Author, metadata.Keywords, metadata.Description),
            sections.Select(s => new SourceSection(s.Id, s.Priority, s.IsActive,
                elements[s.Id].Select(e => new SourceElement(e.Id, e.ElementType, e.Size, e.IsActive, e.TinyText, e.EditorText)).ToList())).ToList());

        public string Fingerprint => _fingerprint ??= SourceFingerprint.Compute(Source);
    }
}
