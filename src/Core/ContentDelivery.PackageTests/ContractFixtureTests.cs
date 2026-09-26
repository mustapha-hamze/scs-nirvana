using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cms.ContentDelivery.PackageTests;

// Generic delivery scenarios (docs/Content-Delivery-Discovery.md §3), run through the packaged
// public contract in a website-style composition: a page composed of ordered blocks, a paginated
// filtered listing, a detail document, fa-IR resolution/fallback and sitemap cultures.
public sealed class ContractFixtureTests : IAsyncLifetime
{
    private const int Tenant = 1, OtherTenant = 2;
    private const int FaIR = 1, EnUS = 2;
    private const int BlockType = 1, ArticleType = 2;
    private const int RootCategory = 10, Category = 11, InactiveCategory = 12, OtherTenantCategory = 20, Tag = 30;
    private static readonly DateTime Base = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly ContentSeed Block1 = new(101, Tenant, BlockType, "Block one", Base)
    {
        HeadLine = "Block one headline",
        Metadata = new(1001, "Block one meta"),
        Sections =
        [
            new(1101, 2, [new(11012, 1, "b"), new(11011, 1, "a")]),
            new(1102, 1, [new(11021, 2, null, "<p>c</p>"), new(11022, 1, "inactive", IsActive: false)]),
            new(1103, 0, [new(11031, 1, "in inactive section")], IsActive: false)
        ],
        Images = [new(5102, "b.jpg", 640), new(5101, "a.jpg", 640)],
        CategoryIds = [Category],
        TagIds = [Tag]
    };

    private static readonly ContentSeed Block2 = new(102, Tenant, BlockType, "Block two", Base)
    {
        Sections = [new(1201, 1, [new(12011, 1, "two")])],
        LegacySnapshot = """{"Id":102,"Title":"[legacy] Block two"}"""
    };

    private static readonly ContentSeed Block3 = new(103, Tenant, BlockType, "Block three", Base);
    private static readonly ContentSeed Block4 = new(104, Tenant, BlockType, "Block four", Base);

    // 25 articles in Category, the first 10 also tagged; UpdatedAt repeats so Id breaks ties.
    private static readonly ContentSeed[] Articles = Enumerable.Range(301, 25)
        .Select(id => new ContentSeed(id, Tenant, ArticleType, $"Article {id}", Base.AddHours(id % 4))
        {
            Images = [new(id * 10, $"{id}.jpg", 640)],
            CategoryIds = [Category],
            TagIds = id <= 310 ? [Tag] : []
        })
        .ToArray();

    private readonly CmsDatabase _db = new();
    private readonly CommandCounter _commands = new();
    private ConsumerHost _host = null!;

    public Task InitializeAsync()
    {
        _host = ConsumerHost.For(_db, Tenant, counter: _commands);

        _db.Culture(FaIR, 0, "fa-IR");
        _db.Culture(EnUS, 0, "en-US");
        _db.Culture(3, Tenant, "de-DE"); // tenant-owned rows are not delivery cultures
        _db.Category(RootCategory, Tenant, 0, "Root");
        _db.Category(Category, Tenant, RootCategory, "Topic");
        _db.Category(InactiveCategory, Tenant, 0, "Hidden", isActive: false);
        _db.Category(OtherTenantCategory, OtherTenant, 0, "Other");
        _db.Tag(Tag, Tenant, "Tag");

        foreach (var content in new[] { Block1, Block2, Block3, Block4 }.Concat(Articles))
            _db.Content(content);
        _db.Content(new(105, Tenant, BlockType, "Deleted", Base) { IsDeleted = true });
        _db.Content(new(106, Tenant, BlockType, "Inactive", Base) { IsActive = false });
        _db.Content(new(201, OtherTenant, BlockType, "Other tenant", Base));
        _db.Content(new(326, Tenant, ArticleType, "Uncategorized", Base));
        _db.Content(new(327, Tenant, ArticleType, "Deleted article", Base) { IsDeleted = true, CategoryIds = [Category] });
        _db.Content(new(401, OtherTenant, ArticleType, "Foreign article", Base) { CategoryIds = [Category] });

        _db.Translation(1, Block1, FaIR, text => $"[fa] {text}");
        _db.Translation(2, Block3, FaIR, text => $"[fa] {text}", fingerprint: "stale");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        _db.Dispose();
    }

    [Fact]
    public async Task Composition_BindsOneApplication_AndItsOptInHealthChecks()
    {
        _host.Services.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(Tenant, _host.Services.GetRequiredService<IOptions<ContentDeliveryOptions>>().Value.ApplicationId);

        var report = await _host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(r => r.Tags.Contains("content-delivery"));
        Assert.Equal(["content-delivery-configuration", "content-delivery-database"], report.Entries.Keys.Order());
        Assert.Equal(HealthStatus.Healthy, report.Status);

        await using var unbound = new ConsumerHost(_db, new Dictionary<string, string?>());
        Assert.Throws<OptionsValidationException>(() => unbound.Services.GetRequiredService<IStartupValidator>().Validate());
        var unboundReport = await unbound.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(r => r.Name == "content-delivery-configuration");
        Assert.Equal(HealthStatus.Unhealthy, unboundReport.Status);
    }

    // A page composed of ordered blocks: requested order, unknown/deleted/inactive/foreign ids
    // dropped, duplicates once, each block its own localization - and no per-block round trips.
    [Fact]
    public async Task BlockComposition()
    {
        await using var legacy = ConsumerHost.For(_db, Tenant, legacyCulture: "fa-IR", counter: _commands);

        var start = _commands.Count;
        var page = await legacy.Read(c => c.GetDocumentSetAsync([103, 999, 101, 201, 105, 106, 102, 101], "fa-IR"));
        var pageCommands = _commands.Count - start;

        Assert.True(page.TryGetValue(out var blocks));
        Assert.Equal([103, 101, 102], blocks.Select(b => b.Summary.Id));
        Assert.Equal([LocalizationSource.Source, LocalizationSource.Translation, LocalizationSource.LegacyFarsi], blocks.Select(b => b.Summary.Localization.Source));
        Assert.Equal(["Block three", "[fa] Block one", "[legacy] Block two"], blocks.Select(b => b.Summary.Title));

        start = _commands.Count;
        await legacy.Read(c => c.GetDocumentSetAsync([104], "fa-IR"));
        Assert.Equal(_commands.Count - start, pageCommands);

        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await _host.Read(c => c.GetDocumentSetAsync([101], "not a culture"))).Status);
        Assert.Empty((await _host.Read(c => c.GetDocumentSetAsync([999]))).Value!);
        await Assert.ThrowsAsync<ArgumentException>(() => _host.Read(c => c.GetDocumentSetAsync(Enumerable.Range(1, ContentDeliveryLimits.MaxDocumentSetSize + 1).ToList())));
    }

    [Fact]
    public async Task FilteredListing_Paginates()
    {
        var expected = Articles.OrderByDescending(a => a.UpdatedAt).ThenByDescending(a => a.Id).Select(a => a.Id).ToList();

        var pages = new List<ContentPage<ContentSummary>>();
        for (var number = 1; number <= 4; number++)
            pages.Add((await _host.Read(c => c.GetListingAsync(new ContentListingQuery { TypeId = ArticleType, CategoryId = Category, PageNumber = number, PageSize = 10 }))).Value!);

        Assert.All(pages, p => Assert.Equal(25, p.TotalCount));
        Assert.Equal([10, 10, 5, 0], pages.Select(p => p.Items.Count));
        Assert.Equal(expected, pages.SelectMany(p => p.Items).Select(i => i.Id));
        Assert.All(pages.SelectMany(p => p.Items), i => Assert.Equal(i.Id * 10, i.PrimaryImage!.Id));

        var tagged = (await _host.Read(c => c.GetListingAsync(new ContentListingQuery { TypeId = ArticleType, CategoryId = Category, TagId = Tag, PageSize = ContentDeliveryLimits.MaxPageSize }))).Value!;
        Assert.Equal(expected.Where(id => id <= 310), tagged.Items.Select(i => i.Id));

        Assert.Equal(26, (await _host.Read(c => c.GetListingAsync(new ContentListingQuery { TypeId = ArticleType }))).Value!.TotalCount);
        foreach (var hidden in new[] { InactiveCategory, OtherTenantCategory, 999 })
            Assert.Equal(0, (await _host.Read(c => c.GetListingAsync(new ContentListingQuery { CategoryId = hidden }))).Value!.TotalCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContentListingQuery { PageSize = ContentDeliveryLimits.MaxPageSize + 1 });

        // A listing heading (category and its parent) comes from the taxonomy, not a second read.
        var taxonomy = await _host.Read(c => c.GetTaxonomyAsync());
        Assert.Equal([RootCategory, Category], taxonomy.Categories.Select(t => t.Id));
        Assert.Equal("Root", taxonomy.Categories.Single(t => t.Id == taxonomy.Categories.Single(c => c.Id == Category).ParentId).Title);
        Assert.Equal([Tag], taxonomy.Tags.Select(t => t.Id));
    }

    [Fact]
    public async Task DetailDocument()
    {
        Assert.True((await _host.Read(c => c.GetDocumentAsync(101))).TryGetValue(out var document));

        Assert.Equal(new LocalizationInfo { Source = LocalizationSource.Source }, document.Summary.Localization);
        Assert.Equal(("Block one", "Block one headline", "Block one meta"), (document.Summary.Title, document.Summary.HeadLine, document.Metadata!.Title));
        Assert.Equal([1102, 1101], document.Sections.Select(s => s.Id));
        Assert.Equal([[11021], [11011, 11012]], document.Sections.Select(s => s.Elements.Select(e => e.Id).ToArray()));
        Assert.Equal("<p>c</p>", document.Sections[0].Elements[0].EditorText);
        Assert.Equal([5101, 5102], document.Images.Select(i => i.Id));
        Assert.Equal(5101, document.Summary.PrimaryImage!.Id);
        Assert.Equal((Category, RootCategory), (document.Categories.Single().Id, document.Categories.Single().ParentId));
        Assert.Equal([Tag], document.Tags.Select(t => t.Id));
        Assert.Equal(document.Summary.Version, (await _host.Read(c => c.GetDocumentAsync(101))).Value!.Summary.Version);

        foreach (var hidden in new[] { 201, 105, 106, 0, -1, 999 })
            Assert.Equal(ContentDeliveryStatus.NotFound, (await _host.Read(c => c.GetDocumentAsync(hidden))).Status);
    }

    [Fact]
    public async Task FaIRResolution_AndFallback()
    {
        var translated = (await _host.Read(c => c.GetDocumentAsync(101, "FA-ir"))).Value!;
        Assert.Equal(new LocalizationInfo { Culture = "fa-IR", Source = LocalizationSource.Translation }, translated.Summary.Localization);
        Assert.Equal(("[fa] Block one", "[fa] Block one meta", "[fa] a", "[fa] <p>c</p>"),
            (translated.Summary.Title, translated.Metadata!.Title, translated.Sections[1].Elements[0].TinyText, translated.Sections[0].Elements[0].EditorText));
        // Only text is localized: layout, ids, element titles and media stay the master's.
        var source = (await _host.Read(c => c.GetDocumentAsync(101))).Value!;
        Assert.Equal(source.Sections.SelectMany(s => s.Elements).Select(e => (e.Id, e.Title, e.ElementType)),
            translated.Sections.SelectMany(s => s.Elements).Select(e => (e.Id, e.Title, e.ElementType)));
        Assert.Equal(source.Images, translated.Images);
        Assert.NotEqual(source.Summary.Version.Tag, translated.Summary.Version.Tag);

        // Stale translation, no translation, legacy snapshot without the legacy fallback: source.
        foreach (var id in new[] { 103, 104, 102 })
        {
            var fallback = (await _host.Read(c => c.GetDocumentAsync(id, "fa-IR"))).Value!.Summary;
            Assert.Equal(new LocalizationInfo { Culture = "fa-IR", Source = LocalizationSource.Source }, fallback.Localization);
        }
        Assert.Equal(new LocalizationInfo { Culture = "en-US", Source = LocalizationSource.Source }, (await _host.Read(c => c.GetDocumentAsync(101, "en-US"))).Value!.Summary.Localization);

        foreach (var invalid in new[] { "not a culture", "fa-IR\n", "de-DE", "fr-FR" })
            Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await _host.Read(c => c.GetDocumentAsync(101, invalid))).Status);

        var listed = (await _host.Read(c => c.GetListingAsync(new ContentListingQuery { TypeId = BlockType, Culture = "fa-IR" }))).Value!;
        Assert.Equal([(104, LocalizationSource.Source), (103, LocalizationSource.Source), (102, LocalizationSource.Source), (101, LocalizationSource.Translation)],
            listed.Items.Select(i => (i.Id, i.Localization.Source)));
    }

    [Fact]
    public async Task SitemapCultures()
    {
        var entries = await _host.Read(c => c.GetSitemapEntriesAsync());

        Assert.Equal(new[] { 101, 102, 103, 104 }.Concat(Enumerable.Range(301, 26)), entries.Select(e => e.ContentId));
        Assert.Equal(["fa-IR"], entries.Single(e => e.ContentId == 101).Cultures);
        Assert.All(entries.Where(e => e.ContentId != 101), e => Assert.Empty(e.Cultures));
        Assert.Equal(Base.AddHours(302 % 4), entries.Single(e => e.ContentId == 302).LastModified);

        await using var legacy = ConsumerHost.For(_db, Tenant, legacyCulture: "fa-IR");
        var legacyEntries = await legacy.Read(c => c.GetSitemapEntriesAsync());
        Assert.Equal(["fa-IR"], legacyEntries.Single(e => e.ContentId == 102).Cultures);
    }

    [Fact]
    public async Task CachedComposition_ServesRepeatReadsWithoutQueries()
    {
        await using var cached = ConsumerHost.For(_db, Tenant, cacheEnabled: true, counter: _commands);

        var first = await cached.Read(c => c.GetDocumentSetAsync([101, 102], "fa-IR"));
        var start = _commands.Count;
        var second = await cached.Read(c => c.GetDocumentSetAsync([101, 102], "fa-IR"));

        Assert.Equal(0, _commands.Count - start);
        Assert.Equal(first.Value!.Select(d => d.Summary), second.Value!.Select(d => d.Summary));
    }
}
