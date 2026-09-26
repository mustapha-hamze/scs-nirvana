using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Cms.ContentDelivery;
using Cms.ContentDelivery.SqlServer;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.ContentDelivery;

// Phase 3 localized resolution through the real SQL adapter over the Core schema. Translations are
// fingerprinted with Application's ContentSourceFingerprint over the graph the translation
// workflow loads, and payloads are written with Application's serializer - so acceptance here
// means delivery agrees with the workflow, not with its own copy of the rules.
public sealed class LocalizedContentDeliveryTests : IDisposable
{
    private const int Tenant = 1;
    private const int OtherTenant = 2;
    private const int FaIR = 1, EnUS = 2, DeDEInactive = 3, FrFRDeleted = 4, ItITTenantOwned = 5, ArSA = 6;

    private static readonly DateTime Jan1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private readonly SqliteConnection _connection;
    private readonly List<ContentDeliveryDbContext> _contexts = new();
    private readonly Dictionary<int, string> _fingerprints = new();

    // Tenant 1 (all fa-IR unless noted):
    // 10 current Ready translation (+ current ar-SA)  11 stale Ready + valid legacy snapshot
    // 12 current NeedsReview   13 current Ready but deleted   14 current ar-SA only
    // 15 current Ready, malformed JSON   16 current Ready, a section missing   17 invalid legacy
    // 18 inactive content with a current translation
    // Tenant 2: 20 current Ready translation.
    public LocalizedContentDeliveryTests()
    {
        using (var core = _factory.CreateContext())
        {
            _connection = (SqliteConnection)core.Database.GetDbConnection();
            Culture Culture(int id, string key, int app = 0, bool active = true, bool deleted = false) =>
                new() { Id = id, ApplicationId = app, Key = key, Title = key, IsActive = active, IsDeleted = deleted, UpdatedDT = Jan1, CreatedDT = Jan1 };
            core.AddRange(Culture(FaIR, "fa-IR"), Culture(EnUS, "en-US"), Culture(DeDEInactive, "de-DE", active: false),
                Culture(FrFRDeleted, "fr-FR", deleted: true), Culture(ItITTenantOwned, "it-IT", app: Tenant), Culture(ArSA, "ar-SA"));

            core.AddRange(Master(10), Master(11, farsi: Legacy11), Master(12), Master(13), Master(14), Master(15), Master(16),
                Master(17, farsi: "{\"Id\":999,\"Title\":\"wrong content\"}"), Master(18, active: false), Master(20, OtherTenant));
            core.SaveChanges();
        }

        foreach (var id in new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 20 })
            _fingerprints[id] = WorkflowFingerprint(id);

        using (var core = _factory.CreateContext())
        {
            var incomplete = Payload(16) with { Sections = Payload(16).Sections.Take(1).ToList() };
            core.AddRange(
                Translation(10, FaIR), Translation(10, ArSA, payload: Payload(10, "AR")),
                Translation(11, FaIR, fingerprint: new string('0', 64)),
                Translation(12, FaIR, status: TranslationStatus.NeedsReview),
                Translation(13, FaIR, deleted: true),
                Translation(14, ArSA, payload: Payload(14, "AR")),
                Translation(15, FaIR, json: "{\"Title\":\"FA\","),
                Translation(16, FaIR, json: LegacyFarsiContentParser.Serialize(incomplete)),
                Translation(18, FaIR),
                Translation(20, FaIR));
            core.SaveChanges();
        }
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
            context.Dispose();
        _factory.Dispose();
    }

    // Partial snapshot: some text supplied, some explicitly null, the rest omitted (HeadLine,
    // Description, metadata Keywords/Description, 1101's TinyText, 1102's TinyText).
    private const string Legacy11 = """
        {"Id":11,"Title":"Legacy 11","Abstract":null,
         "Metadata":{"Id":110,"ContentId":11,"Title":"Legacy meta","Author":null},
         "Sections":[{"Id":111,"ContentId":11,"Elements":[
           {"Id":1101,"SectionId":111,"EditorText":"legacy editor"},
           {"Id":1102,"SectionId":111,"EditorText":null}]}]}
        """;

    // Master graph: an active section (a tiny-text, an editor-text, an inactive and a deleted
    // element) and an inactive section. Inactive nodes are fingerprinted but never delivered.
    private static Content Master(int id, int app = Tenant, string farsi = null, bool active = true) => new()
    {
        Id = id, ApplicationId = app, TypeId = 1000, IsActive = active, FarsiContent = farsi,
        Title = $"Title {id}", HeadLine = $"Head {id}", Abstract = $"Abstract {id}", Description = $"<p>Description {id}</p>",
        PublishDt = Jan1, UpdatedDT = Jan1.AddDays(id), CreatedDT = Jan1,
        Metadata = new ContentMetadata { Id = id * 10, Title = $"Meta {id}", Author = "Author", Keywords = "k", Description = "md", UpdatedDT = Jan1, CreatedDT = Jan1 },
        Images = new List<ContentImage> { new() { Id = id * 10 + 5, ImageFileName = $"i{id}.jpg", Size = 640, IsActive = true, UpdatedDT = Jan1, CreatedDT = Jan1 } },
        Sections = new List<ContentSection>
        {
            new()
            {
                Id = id * 10 + 1, Priority = 1, IsActive = true, UpdatedDT = Jan1, CreatedDT = Jan1,
                Elements = new List<SectionElement>
                {
                    new() { Id = id * 100 + 1, ElementType = 1000, IsActive = true, TinyText = $"tiny {id}", ElementTitle = "E1", FileNameText = "f.pdf", GalleryImages = "g", Size = 3, UpdatedDT = Jan1, CreatedDT = Jan1 },
                    new() { Id = id * 100 + 2, ElementType = 1002, IsActive = true, EditorText = $"<p>body {id}</p>", UpdatedDT = Jan1, CreatedDT = Jan1 },
                    new() { Id = id * 100 + 4, ElementType = 1000, IsActive = false, TinyText = "inactive", UpdatedDT = Jan1, CreatedDT = Jan1 },
                    new() { Id = id * 100 + 5, ElementType = 1000, IsActive = true, IsDeleted = true, TinyText = "deleted", UpdatedDT = Jan1, CreatedDT = Jan1 }
                }
            },
            new()
            {
                Id = id * 10 + 2, Priority = 2, IsActive = false, UpdatedDT = Jan1, CreatedDT = Jan1,
                Elements = new List<SectionElement> { new() { Id = id * 100 + 3, ElementType = 1000, IsActive = true, TinyText = "hidden section", UpdatedDT = Jan1, CreatedDT = Jan1 } }
            }
        }
    };

    // Exactly the non-deleted master tree, inactive nodes included.
    private static LocalizedContentText Payload(int id, string prefix = "FA") => new(
        $"{prefix} title {id}", $"{prefix} head {id}", null, $"<p>{prefix} description {id}</p>",
        new LocalizedMetadataText(id * 10, $"{prefix} meta {id}", $"{prefix} author", "k", "md"),
        new List<LocalizedSectionText>
        {
            new(id * 10 + 1, new List<LocalizedElementText>
            {
                new(id * 100 + 1, $"{prefix} tiny {id}", null),
                new(id * 100 + 2, null, $"<p>{prefix} body {id}</p>"),
                new(id * 100 + 4, $"{prefix} inactive", null)
            }),
            new(id * 10 + 2, new List<LocalizedElementText> { new(id * 100 + 3, $"{prefix} hidden", null) })
        });

    private string WorkflowFingerprint(int contentId)
    {
        using var core = _factory.CreateContext();
        return ContentSourceFingerprint.Compute(new ContentTranslationJobRepository(core).FindSourceGraph(contentId).GetAwaiter().GetResult());
    }

    private int _translationId = 400;

    private ContentTranslation Translation(int contentId, int cultureId, TranslationStatus status = TranslationStatus.Ready, bool deleted = false,
        string fingerprint = null, string json = null, LocalizedContentText payload = null) => new()
    {
        Id = _translationId++, ContentId = contentId, CultureId = cultureId, TranslationStatus = status, IsDeleted = deleted, IsActive = true,
        SourceFingerprint = fingerprint ?? _fingerprints[contentId],
        LocalizedTextJson = json ?? LegacyFarsiContentParser.Serialize(payload ?? Payload(contentId)),
        Provider = "secret-provider", Model = "secret-model", Error = "secret-error", UpdatedDT = Jan1, CreatedDT = Jan1
    };

    private SqlContentDeliveryClient Client(int applicationId = Tenant, string legacyCulture = null)
    {
        var context = new ContentDeliveryDbContext(new DbContextOptionsBuilder<ContentDeliveryDbContext>().UseSqlite(_connection).Options);
        _contexts.Add(context);
        return new SqlContentDeliveryClient(context, new ContentDeliveryTenant(applicationId, legacyCulture));
    }

    private async Task<ContentDocument> Document(int id, string culture, string legacyCulture = null) =>
        (await Client(legacyCulture: legacyCulture).GetDocumentAsync(id, culture)).Value;

    private static void AssertSource(ContentDocument document, int id, string culture)
    {
        Assert.Equal($"Title {id}", document.Summary.Title);
        Assert.Equal($"tiny {id}", document.Sections[0].Elements[0].TinyText);
        Assert.Equal(new LocalizationInfo { Culture = culture, Source = LocalizationSource.Source }, document.Summary.Localization);
    }

    [Fact]
    public async Task CurrentReadyTranslation_LocalizesOnlyTextFields_OverTheMasterGraph()
    {
        var source = await Document(10, null);
        var document = await Document(10, "fa-IR");

        Assert.Equal(new LocalizationInfo { Culture = "fa-IR", Source = LocalizationSource.Translation }, document.Summary.Localization);
        Assert.Equal(("FA title 10", "FA head 10", (string)null), (document.Summary.Title, document.Summary.HeadLine, document.Summary.Abstract));
        Assert.Equal("<p>FA description 10</p>", document.Description);
        Assert.Equal(("FA meta 10", "FA author"), (document.Metadata.Title, document.Metadata.Author));

        // Structure, ids, media and non-text element fields are the master's; inactive/deleted
        // nodes stay hidden even though the payload carries text for the inactive ones.
        Assert.Equal(source.Sections.Select(s => (s.Id, s.Priority)), document.Sections.Select(s => (s.Id, s.Priority)));
        Assert.Equal(new[] { 1001, 1002 }, document.Sections.Single().Elements.Select(e => e.Id));
        var tiny = document.Sections[0].Elements[0];
        Assert.Equal(("FA tiny 10", (string)null), (tiny.TinyText, tiny.EditorText));
        Assert.Equal(("E1", "f.pdf", "g", 3, 1000), (tiny.Title, tiny.FileName, tiny.GalleryImages, tiny.Size, tiny.ElementType));
        Assert.Equal("<p>FA body 10</p>", document.Sections[0].Elements[1].EditorText);
        Assert.Equal(source.Images, document.Images);
        Assert.Equal(source.Summary.PrimaryImage, document.Summary.PrimaryImage);
        Assert.Equal(source.Summary.PublishedAt, document.Summary.PublishedAt);
        Assert.NotEqual(source.Summary.Version.Tag, document.Summary.Version.Tag);
    }

    [Theory]
    [InlineData("FA-ir")]
    [InlineData("fa-ir")]
    [InlineData("FA-IR")]
    public async Task Culture_MatchesIgnoringCase_AndReportsTheStoredKey(string culture)
    {
        var document = await Document(10, culture);

        Assert.Equal("fa-IR", document.Summary.Localization.Culture);
        Assert.Equal(LocalizationSource.Translation, document.Summary.Localization.Source);
    }

    [Theory]
    [InlineData("de-DE")]  // inactive
    [InlineData("fr-FR")]  // deleted
    [InlineData("it-IT")]  // owned by the tenant, not global
    [InlineData("xx-YY")]  // unknown
    [InlineData("fa_IR")]  // malformed
    public async Task NonGlobalOrUnavailableCulture_IsInvalid_ForEveryShape(string culture)
    {
        var client = Client();

        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await client.GetDocumentAsync(10, culture)).Status);
        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await client.GetDocumentSetAsync(new[] { 10 }, culture)).Status);
        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await client.GetListingAsync(new ContentListingQuery { Culture = culture })).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task NoCulture_IsASourceRead(string culture)
    {
        AssertSource(await Document(10, culture), 10, null);
    }

    [Theory]
    [InlineData(11)] // stale fingerprint (legacy fallback off)
    [InlineData(12)] // NeedsReview
    [InlineData(13)] // deleted
    [InlineData(14)] // only another culture's row
    [InlineData(15)] // malformed JSON
    [InlineData(16)] // payload missing a master section
    public async Task UnusableTranslation_FallsBackToSource_WithTheResolvedCulture(int contentId)
    {
        AssertSource(await Document(contentId, "fa-IR"), contentId, "fa-IR");
    }

    [Fact]
    public async Task ActiveCultureWithoutTranslation_ServesSource()
    {
        AssertSource(await Document(10, "en-US"), 10, "en-US");
    }

    [Fact]
    public async Task EachCulture_ReadsOnlyItsOwnRow()
    {
        Assert.Equal("AR title 14", (await Document(14, "ar-SA")).Summary.Title);
        Assert.Equal("AR title 10", (await Document(10, "ar-SA")).Summary.Title);
        Assert.Equal("FA title 10", (await Document(10, "fa-IR")).Summary.Title);
    }

    [Fact]
    public async Task ChangedSourceText_MakesTheTranslationStale()
    {
        using (var core = _factory.CreateContext())
        {
            core.SectionElements.Single(e => e.Id == 1002).EditorText = "<p>new body</p>";
            core.SaveChanges();
        }

        AssertSource(await Document(10, "fa-IR"), 10, "fa-IR");
    }

    [Fact]
    public async Task LegacyFallback_OverlaysAPartialSnapshot_FieldByField()
    {
        var client = Client(legacyCulture: "FA-IR");
        var legacy = (await client.GetDocumentAsync(11, "fa-IR")).Value;
        var inSet = (await client.GetDocumentSetAsync(new[] { 10, 11 }, "fa-IR")).Value.Single(d => d.Summary.Id == 11);
        var listed = (await client.GetListingAsync(new ContentListingQuery { Culture = "fa-IR", PageSize = 100 })).Value.Items.Single(i => i.Id == 11);

        foreach (var document in new[] { legacy, inSet })
        {
            Assert.Equal(new LocalizationInfo { Culture = "fa-IR", Source = LocalizationSource.LegacyFarsi }, document.Summary.Localization);
            // Supplied -> Farsi, explicit null -> blank, omitted -> English.
            Assert.Equal(("Legacy 11", "Head 11", (string)null), (document.Summary.Title, document.Summary.HeadLine, document.Summary.Abstract));
            Assert.Equal("<p>Description 11</p>", document.Description);
            Assert.Equal(("Legacy meta", (string)null, "k", "md"), (document.Metadata.Title, document.Metadata.Author, document.Metadata.Keywords, document.Metadata.Description));
            var (tiny, editor) = (document.Sections[0].Elements[0], document.Sections[0].Elements[1]);
            Assert.Equal(("tiny 11", "legacy editor"), (tiny.TinyText, tiny.EditorText));
            Assert.Equal(((string)null, (string)null), (editor.TinyText, editor.EditorText));
            Assert.Equal(("E1", "f.pdf", "g", 3), (tiny.Title, tiny.FileName, tiny.GalleryImages, tiny.Size));
        }

        Assert.Equal(new LocalizationInfo { Culture = "fa-IR", Source = LocalizationSource.LegacyFarsi }, listed.Localization);
        Assert.Equal(("Legacy 11", "Head 11", (string)null), (listed.Title, listed.HeadLine, listed.Abstract));
    }

    [Fact]
    public async Task LegacyFallback_IsGatedByConfiguration_AndCulture()
    {
        // Off by default, and never for another culture.
        AssertSource(await Document(11, "fa-IR"), 11, "fa-IR");
        AssertSource(await Document(11, "ar-SA", legacyCulture: "fa-IR"), 11, "ar-SA");
        // A current canonical translation still wins.
        Assert.Equal(LocalizationSource.Translation, (await Document(10, "fa-IR", legacyCulture: "fa-IR")).Summary.Localization.Source);
    }

    [Theory]
    [InlineData("{\"Id\":999,\"Title\":\"wrong content\"}")]                                              // another content
    [InlineData("{\"Id\":\"17\",\"Title\":\"x\"}")]                                                        // invalid id
    [InlineData("{\"Id\":17,\"Title\":\"x\"")]                                                             // malformed JSON
    [InlineData("{\"Id\":17,\"Title\":5}")]                                                                // wrong type
    [InlineData("{\"Id\":17,\"HeadLine\":\"x\",\"headline\":\"y\"}")]                                        // duplicate property
    [InlineData("{\"Id\":17,\"Metadata\":{\"Id\":999,\"Title\":\"x\"}}")]                                    // unknown metadata
    [InlineData("{\"Id\":17,\"Metadata\":{\"Id\":170,\"Author\":[\"x\"]}}")]                                  // wrong metadata type
    [InlineData("{\"Id\":17,\"Sections\":[{\"Id\":999}]}")]                                                 // unknown section
    [InlineData("{\"Id\":17,\"Sections\":[{\"Id\":171},{\"Id\":171}]}")]                                     // duplicate section
    [InlineData("{\"Id\":17,\"Sections\":[{\"Id\":171,\"ContentId\":16}]}")]                                 // wrong parent content
    [InlineData("{\"Id\":17,\"Sections\":[{\"Id\":171,\"Elements\":[{\"Id\":1799}]}]}")]                    // unknown element
    [InlineData("{\"Id\":17,\"Sections\":[{\"Id\":171,\"Elements\":[{\"Id\":1705}]}]}")]                    // deleted element
    [InlineData("{\"Id\":17,\"Sections\":[{\"Id\":171,\"Elements\":[{\"Id\":1701},{\"Id\":1701}]}]}")]     // duplicate element
    [InlineData("{\"Id\":17,\"Sections\":[{\"Id\":171,\"Elements\":[{\"Id\":1701,\"SectionId\":172}]}]}")] // wrong parent section
    [InlineData("{\"Id\":17,\"Sections\":[{\"Id\":171,\"Elements\":[{\"Id\":1701,\"TinyText\":true}]}]}")] // wrong element type
    public async Task InvalidLegacySnapshot_FallsBackToSource_ForEveryShape(string snapshot)
    {
        using (var core = _factory.CreateContext())
        {
            core.Contents.Single(c => c.Id == 17).FarsiContent = snapshot;
            core.SaveChanges();
        }
        var client = Client(legacyCulture: "fa-IR");

        AssertSource((await client.GetDocumentAsync(17, "fa-IR")).Value, 17, "fa-IR");
        AssertSource((await client.GetDocumentSetAsync(new[] { 17 }, "fa-IR")).Value.Single(), 17, "fa-IR");
        var listed = (await client.GetListingAsync(new ContentListingQuery { Culture = "fa-IR", PageSize = 100 })).Value.Items.Single(i => i.Id == 17);
        Assert.Equal(("Title 17", "Head 17", "Abstract 17"), (listed.Title, listed.HeadLine, listed.Abstract));
        Assert.Equal(new LocalizationInfo { Culture = "fa-IR", Source = LocalizationSource.Source }, listed.Localization);
        Assert.Empty((await client.GetSitemapEntriesAsync()).Single(e => e.ContentId == 17).Cultures);
    }

    [Fact]
    public async Task DocumentSetAndListing_ResolveEachItem()
    {
        var set = (await Client().GetDocumentSetAsync(new[] { 12, 10, 14 }, "fa-IR")).Value;
        Assert.Equal(new[] { ("Title 12", LocalizationSource.Source), ("FA title 10", LocalizationSource.Translation), ("Title 14", LocalizationSource.Source) },
            set.Select(d => (d.Summary.Title, d.Summary.Localization.Source)));

        var page = (await Client(legacyCulture: "fa-IR").GetListingAsync(new ContentListingQuery { Culture = "fa-IR", PageSize = 100 })).Value;
        var byId = page.Items.ToDictionary(i => i.Id);
        // Paging/order unchanged: UpdatedDT desc = id desc here.
        Assert.Equal(new[] { 17, 16, 15, 14, 13, 12, 11, 10 }, page.Items.Select(i => i.Id));
        Assert.Equal(("FA title 10", "FA head 10", LocalizationSource.Translation), (byId[10].Title, byId[10].HeadLine, byId[10].Localization.Source));
        Assert.Equal(("Legacy 11", LocalizationSource.LegacyFarsi), (byId[11].Title, byId[11].Localization.Source));
        Assert.All(page.Items.Where(i => i.Id > 11), i => Assert.Equal((LocalizationSource.Source, "fa-IR"), (i.Localization.Source, i.Localization.Culture)));
        Assert.Equal(105, byId[10].PrimaryImage.Id);
    }

    [Fact]
    public async Task Sitemap_ListsOnlyCulturesWithAServableTranslation_InKeyOrder()
    {
        var entries = (await Client().GetSitemapEntriesAsync()).ToDictionary(e => e.ContentId, e => e.Cultures);

        Assert.Equal(new[] { 10, 11, 12, 13, 14, 15, 16, 17 }, entries.Keys.OrderBy(k => k));
        Assert.Equal(new[] { "ar-SA", "fa-IR" }, entries[10]);
        Assert.Equal(new[] { "ar-SA" }, entries[14]);
        Assert.All(new[] { 11, 12, 13, 15, 16, 17 }, id => Assert.Empty(entries[id]));

        var withLegacy = (await Client(legacyCulture: "fa-IR").GetSitemapEntriesAsync()).ToDictionary(e => e.ContentId, e => e.Cultures);
        Assert.Equal(new[] { "fa-IR" }, withLegacy[11]);
        Assert.Empty(withLegacy[17]);
    }

    [Fact]
    public async Task Tenants_NeverSeeEachOthersTranslations()
    {
        Assert.Equal(ContentDeliveryStatus.NotFound, (await Client().GetDocumentAsync(20, "fa-IR")).Status);
        Assert.Equal(ContentDeliveryStatus.NotFound, (await Client().GetDocumentAsync(18, "fa-IR")).Status);

        var other = Client(OtherTenant);
        Assert.Equal("FA title 20", (await other.GetDocumentAsync(20, "fa-IR")).Value.Summary.Title);
        Assert.Equal(new[] { "fa-IR" }, (await other.GetSitemapEntriesAsync()).Single().Cultures);
        Assert.Equal(ContentDeliveryStatus.NotFound, (await other.GetDocumentAsync(10, "fa-IR")).Status);
    }

    [Fact]
    public async Task Results_CarryNoTranslationInternals()
    {
        var client = Client(legacyCulture: "fa-IR");
        var json = JsonSerializer.Serialize(new object[]
        {
            (await client.GetDocumentSetAsync(new[] { 10, 11 }, "fa-IR")).Value,
            (await client.GetListingAsync(new ContentListingQuery { Culture = "fa-IR" })).Value,
            await client.GetSitemapEntriesAsync()
        });

        Assert.DoesNotContain(_fingerprints[10], json);
        foreach (var secret in new[] { "secret-", "LocalizedTextJson", "Fingerprint", "\"Id\":11,\"Title\":\"Legacy" })
            Assert.DoesNotContain(secret, json);
    }

    [Fact]
    public async Task LocalizedReads_WriteNothing()
    {
        long Changes()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT total_changes();";
            return (long)command.ExecuteScalar();
        }

        var before = Changes();
        var client = Client(legacyCulture: "fa-IR");

        await client.GetDocumentAsync(11, "fa-IR");
        await client.GetDocumentSetAsync(new[] { 10, 15, 16 }, "FA-ir");
        await client.GetListingAsync(new ContentListingQuery { Culture = "ar-SA" });
        await client.GetSitemapEntriesAsync();

        Assert.Equal(before, Changes());
        Assert.All(_contexts, c => Assert.Empty(c.ChangeTracker.Entries()));
    }
}

// The translation workflow's own write path (manual translation over the real repositories)
// must produce rows delivery accepts - and a later source edit must retire them.
public sealed class ContentDeliveryWorkflowCompatibilityTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ManualTranslation_IsServed_UntilTheSourceChanges()
    {
        int contentId, cultureId;
        await using (var core = _factory.CreateContext())
        {
            var culture = new Culture { ApplicationId = 0, Key = "fa-IR", Title = "Farsi", IsActive = true };
            var content = new Content
            {
                ApplicationId = 1, TypeId = 1000, IsActive = true, Title = "English", HeadLine = "Head",
                Metadata = new ContentMetadata { Title = "Meta" },
                Sections = new List<ContentSection>
                {
                    new() { Priority = 1, IsActive = true, Elements = new List<SectionElement> { new() { ElementType = 1000, TinyText = "tiny", IsActive = true }, new() { ElementType = 1002, EditorText = "<p>body</p>", IsActive = true } } },
                    new() { Priority = 2, IsActive = false, Elements = new List<SectionElement>() }
                }
            };
            core.AddRange(culture, content);
            await core.SaveChangesAsync();
            (contentId, cultureId) = (content.Id, culture.Id);
        }

        await using (var core = _factory.CreateContext())
        {
            var workflow = new ManualContentTranslation(new ContentTranslationJobRepository(core), TimeProvider.System);
            var editor = await workflow.GetEditor(contentId, cultureId, 1);
            var body = editor.Text.Sections.Single(s => s.Elements.Count == 2);
            var text = editor.Text with
            {
                Title = "عنوان",
                Sections = editor.Text.Sections.Select(s => s == body
                    ? s with { Elements = new List<LocalizedElementText> { s.Elements[0] with { TinyText = "ریز" }, s.Elements[1] with { EditorText = "<p>متن</p>" } } }
                    : s).ToList()
            };
            Assert.Equal(ManualTranslationSaveResult.Saved, await workflow.Save(contentId, cultureId, 1, editor.SourceFingerprint, text));
        }

        async Task<ContentDocument> Deliver()
        {
            await using var core = _factory.CreateContext();
            await using var delivery = new ContentDeliveryDbContext(new DbContextOptionsBuilder<ContentDeliveryDbContext>().UseSqlite(core.Database.GetDbConnection()).Options);
            return (await new SqlContentDeliveryClient(delivery, new ContentDeliveryTenant(1)).GetDocumentAsync(contentId, "fa-IR")).Value;
        }

        var translated = await Deliver();
        Assert.Equal(LocalizationSource.Translation, translated.Summary.Localization.Source);
        Assert.Equal("عنوان", translated.Summary.Title);
        Assert.Equal(new[] { "ریز", null }, translated.Sections.Single().Elements.Select(e => e.TinyText));
        Assert.Equal("<p>متن</p>", translated.Sections.Single().Elements[1].EditorText);

        await using (var core = _factory.CreateContext())
        {
            core.Contents.Single(c => c.Id == contentId).Title = "English, edited";
            await core.SaveChangesAsync();
        }

        var stale = await Deliver();
        Assert.Equal(LocalizationSource.Source, stale.Summary.Localization.Source);
        Assert.Equal("English, edited", stale.Summary.Title);
    }

    // The same vector is asserted on .NET 9 / EF Core 9 in Cms.ContentDelivery.SqlServer.Tests
    // (Net9RuntimeTests.Fingerprint_MatchesTheWorkflowVector), proving both runtimes hash alike.
    public const string VectorFingerprint = "aae13f578397ee8b1941078363bf8047a571908fa4f4d9fff5eab03b06e4b9a0";

    [Fact]
    public void WorkflowFingerprint_MatchesTheSharedVector()
    {
        var content = new Content
        {
            Id = 7, TypeId = 1000, Title = "Title", HeadLine = null, Abstract = "", Description = "<p>شرح & \"x\"</p>",
            Metadata = new ContentMetadata { Id = 3, Title = "Meta", Author = null, Keywords = "k", Description = "d" },
            Sections = new List<ContentSection>
            {
                new() { Id = 20, Priority = 2, IsActive = true, Elements = new List<SectionElement> { new() { Id = 202, ElementType = 1002, Size = 1, IsActive = true, EditorText = "<p>b</p>" }, new() { Id = 201, ElementType = 1000, IsActive = false, TinyText = "a" } } },
                new() { Id = 10, Priority = 1, IsActive = false, Elements = new List<SectionElement>() }
            }
        };

        Assert.Equal(VectorFingerprint, ContentSourceFingerprint.Compute(content));
    }
}
