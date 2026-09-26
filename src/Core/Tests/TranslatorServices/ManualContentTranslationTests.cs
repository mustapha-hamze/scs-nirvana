using Application.CMSRepository;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.TranslatorServices;

// Over SQLite with the real repositories; there is no translation port in the use case at all.
public class ManualContentTranslationTests : IDisposable
{
    private const int ApplicationId = 1;
    private const string LegacyFarsi = "{\"Title\":\"legacy\"}";
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private int _contentId;
    private int _cultureId;

    public void Dispose() => _factory.Dispose();

    private async Task Seed(bool cultureDeleted = false)
    {
        await using var context = _factory.CreateContext();
        var culture = new Culture { ApplicationId = ApplicationId, Title = "Farsi", Key = "fa-IR", IsDeleted = cultureDeleted };
        var content = new Content
        {
            ApplicationId = ApplicationId, TypeId = 1000, Title = "English", HeadLine = "Head", Abstract = null,
            Description = "<p>English <strong>desc</strong></p>", FarsiContent = LegacyFarsi,
            Metadata = new ContentMetadata { Title = "Meta", Author = null },
            Sections = new List<ContentSection>
            {
                new() { Priority = 2, Elements = new List<SectionElement> { new() { ElementType = 1000, TinyText = "tiny" }, new() { ElementType = 1002, EditorText = "<p>body</p>" } } },
                new() { Priority = 1, Elements = new List<SectionElement>() }
            }
        };
        context.AddRange(culture, content);
        await context.SaveChangesAsync();
        (_contentId, _cultureId) = (content.Id, culture.Id);
    }

    // What the editor posts: the text it rendered (seeded from source here) with translated text,
    // only in the fields each element type edits.
    private async Task<LocalizedContentText> Translated(Func<LocalizedContentText, LocalizedContentText> edit = null)
    {
        var rendered = (await Editor()).Text;
        var body = rendered.Sections.Single(s => s.Elements.Count == 2);
        var text = rendered with
        {
            Title = "عنوان",
            Description = "<p>شرح <strong>کامل</strong></p>",
            Metadata = rendered.Metadata with { Author = "نویسنده" }, // text the source doesn't have
            HeadLine = null, // left blank by the editor
            Sections = rendered.Sections.Select(s => s == body
                ? s with { Elements = new List<LocalizedElementText> { s.Elements[0] with { TinyText = "ریز" }, s.Elements[1] with { EditorText = "<p>متن</p>" } } }
                : s).ToList()
        };
        return edit == null ? text : edit(text);
    }

    // expectedFingerprint defaults to the current source's, i.e. an editor loaded just now.
    private async Task<ManualTranslationSaveResult> Save(LocalizedContentText text, int? cultureId = null, int applicationId = ApplicationId, string expectedFingerprint = null)
    {
        expectedFingerprint ??= await Fingerprint();
        await using var context = _factory.CreateContext();
        return await Sut(context).Save(_contentId, cultureId ?? _cultureId, applicationId, expectedFingerprint, text);
    }

    private static ManualContentTranslation Sut(ApplicationDbContext context) =>
        new(new ContentTranslationJobRepository(context), new FakeTimeProvider(Now));

    private async Task<T> Read<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        await using var context = _factory.CreateContext();
        return await read(context);
    }

    private Task<string> Legacy() => Read(c => c.Contents.Where(x => x.Id == _contentId).Select(x => x.FarsiContent).SingleAsync());

    private Task<DateTime> ContentUpdated() => Read(c => c.Contents.Where(x => x.Id == _contentId).Select(x => x.UpdatedDT).SingleAsync());

    private Task<ContentTranslation> Row() => Read(c => c.ContentTranslations.IgnoreQueryFilters().SingleOrDefaultAsync());

    private async Task<string> Fingerprint()
    {
        await using var context = _factory.CreateContext();
        return ContentSourceFingerprint.Compute(await new ContentTranslationJobRepository(context).FindSourceGraph(_contentId));
    }

    private async Task AddRow(TranslationStatus status, bool deleted = false)
    {
        await using var context = _factory.CreateContext();
        context.ContentTranslations.Add(new ContentTranslation
        {
            ContentId = _contentId, CultureId = _cultureId, TranslationStatus = status, SourceFingerprint = "old",
            LocalizedTextJson = "{}", Provider = ContentTranslationBackfill.LegacyProvider, Error = "x", IsActive = true, IsDeleted = deleted
        });
        await context.SaveChangesAsync();
    }

    private async Task AssertNothingWritten(TranslationStatus? existingStatus)
    {
        Assert.Equal(LegacyFarsi, await Legacy());
        var row = await Row();
        if (existingStatus == null)
            Assert.Null(row);
        else
            Assert.Equal((existingStatus.Value, "old", "{}"), (row.TranslationStatus, row.SourceFingerprint, row.LocalizedTextJson));
    }

    [Fact]
    public async Task ValidSave_WritesOnlyCurrentReadyTranslation_AndIsImmediatelyActivatable()
    {
        await Seed();
        await AddRow(TranslationStatus.Stale);
        var updated = await ContentUpdated();

        Assert.Equal(ManualTranslationSaveResult.Saved, await Save(await Translated()));

        // Legacy FarsiContent keeps its exact bytes and the content row isn't touched.
        Assert.Equal((LegacyFarsi, updated), (await Legacy(), await ContentUpdated()));
        Assert.False(await Read(c => c.ContentTranslationJobs.AnyAsync()));
        var row = await Row();
        Assert.Equal((TranslationStatus.Ready, await Fingerprint(), ManualContentTranslation.Provider, null, Now, null),
            (row.TranslationStatus, row.SourceFingerprint, row.Provider, row.Model, row.TranslatedAt, row.Error));
        var text = LegacyFarsiContentParser.Deserialize(row.LocalizedTextJson);
        Assert.Equal(("عنوان", null, "<p>شرح <strong>کامل</strong></p>", "نویسنده"), (text.Title, text.HeadLine, text.Description, text.Metadata.Author));
        // Sections follow master order (priority), not the editor's.
        Assert.Equal(2, text.Sections.Count);
        Assert.Empty(text.Sections[0].Elements);
        Assert.Equal(new[] { ("ریز", (string)null), (null, "<p>متن</p>") }, text.Sections[1].Elements.Select(e => (e.TinyText, e.EditorText)));

        var state = await Read(c => new ContentTranslationRequests(new ContentTranslationJobRepository(c), new FakeTimeProvider(Now)).GetState(_contentId, _cultureId, ApplicationId));
        Assert.Equal(ContentTranslationState.Ready, state);
    }

    [Fact]
    public async Task ValidSave_SectionsInAnyOrder_AreStoredInMasterOrder()
    {
        await Seed();

        Assert.Equal(ManualTranslationSaveResult.Saved, await Save(await Translated(t => t with { Sections = Enumerable.Reverse(t.Sections).ToList() })));

        Assert.Empty(LegacyFarsiContentParser.Deserialize((await Row()).LocalizedTextJson).Sections[0].Elements);
    }

    private static LocalizedSectionText Body(LocalizedContentText t) => t.Sections.Single(s => s.Elements.Count == 2);

    private static LocalizedContentText WithBody(LocalizedContentText t, Func<LocalizedSectionText, LocalizedSectionText> edit) =>
        t with { Sections = t.Sections.Select(s => s == Body(t) ? edit(s) : s).ToList() };

    public static TheoryData<string, Func<LocalizedContentText, LocalizedContentText>> InvalidEdits => new()
    {
        { "extra section (deleted since the editor loaded)", t => t with { Sections = t.Sections.Append(new LocalizedSectionText(999, new List<LocalizedElementText>())).ToList() } },
        { "missing section", t => t with { Sections = t.Sections.Skip(1).ToList() } },
        { "no sections", t => t with { Sections = null } },
        { "missing element", t => WithBody(t, s => s with { Elements = s.Elements.Skip(1).ToList() }) },
        { "extra element", t => WithBody(t, s => s with { Elements = s.Elements.Append(new LocalizedElementText(999, "x", null)).ToList() }) },
        { "duplicate element", t => WithBody(t, s => s with { Elements = new List<LocalizedElementText> { s.Elements[0], s.Elements[0] } }) },
        { "null element", t => WithBody(t, s => s with { Elements = new List<LocalizedElementText> { s.Elements[0], null } }) },
        { "null elements", t => WithBody(t, s => s with { Elements = null }) },
        { "null section", t => t with { Sections = new List<LocalizedSectionText> { Body(t), null } } },
        { "element moved to another section", t => t with
            {
                Sections = t.Sections.Select(s => s == Body(t)
                    ? s with { Elements = s.Elements.Skip(1).ToList() }
                    : s with { Elements = Body(t).Elements.Take(1).ToList() }).ToList()
            } },
        { "wrong metadata id", t => t with { Metadata = t.Metadata with { Id = 999 } } },
        { "missing metadata", t => t with { Metadata = null } },
        { "html structure changed", t => t with { Description = "<p>شرح <em>کامل</em></p>" } },
        { "malformed html", t => t with { Description = "<p>شرح <strong>کامل</strong></p></div>" } },
        { "editor text on a tiny-text element", t => WithBody(t, s => s with { Elements = new List<LocalizedElementText> { s.Elements[0] with { EditorText = "x" }, s.Elements[1] } }) },
        { "tiny text on an editor element", t => WithBody(t, s => s with { Elements = new List<LocalizedElementText> { s.Elements[0], s.Elements[1] with { TinyText = "x" } } }) },
    };

    [Theory]
    [MemberData(nameof(InvalidEdits))]
    public async Task InvalidOrStaleStructure_IsRejected_AndPreservesBothPayloads(string _, Func<LocalizedContentText, LocalizedContentText> edit)
    {
        await Seed();
        await AddRow(TranslationStatus.Stale);

        Assert.Equal(ManualTranslationSaveResult.InvalidStructure, await Save(await Translated(edit)));

        await AssertNothingWritten(TranslationStatus.Stale);
    }

    // A text-only edit keeps the tree valid; only the fingerprint stops a false Ready.
    [Theory]
    [InlineData("text")]
    [InlineData("layout")]
    public async Task SourceEditedAfterEditorLoaded_IsSourceChanged_AndWritesNothing(string change)
    {
        await Seed();
        await AddRow(TranslationStatus.Stale);
        var loadedFingerprint = (await Editor()).SourceFingerprint;
        var text = await Translated();
        await using (var context = _factory.CreateContext())
        {
            if (change == "text")
                (await context.Contents.SingleAsync(c => c.Id == _contentId)).Title = "English edited";
            else
                context.SectionElements.Add(new SectionElement
                {
                    SectionId = (await context.ContentSections.FirstAsync(s => s.ContentId == _contentId && s.Priority == 1)).Id, ElementType = 1000, TinyText = "new"
                });
            await context.SaveChangesAsync();
        }

        Assert.Equal(ManualTranslationSaveResult.SourceChanged, await Save(text, expectedFingerprint: loadedFingerprint));

        await AssertNothingWritten(TranslationStatus.Stale);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task MissingOrForeignFingerprint_IsSourceChanged_AndWritesNothing(string expected)
    {
        await Seed();

        Assert.Equal(ManualTranslationSaveResult.SourceChanged, await Save(await Translated(), expectedFingerprint: expected));

        await AssertNothingWritten(null);
    }

    [Theory]
    [InlineData("unconfigured")]
    [InlineData("missing")]
    [InlineData("deleted")]
    public async Task UnavailableCulture_WritesNothing(string scenario)
    {
        await Seed(cultureDeleted: scenario == "deleted");
        var cultureId = scenario switch { "unconfigured" => 0, "missing" => 999, _ => _cultureId };

        Assert.Equal(ManualTranslationSaveResult.CultureUnavailable, await Save(await Translated(), cultureId));

        await AssertNothingWritten(null);
    }

    [Fact]
    public async Task OtherApplicationsContent_IsNotFound_AndWritesNothing()
    {
        await Seed();

        Assert.Equal(ManualTranslationSaveResult.NotFound, await Save(await Translated(), applicationId: 2));

        await AssertNothingWritten(null);
    }

    [Fact]
    public async Task SoftDeletedTranslation_IsNotResurrected_AndWritesNothing()
    {
        await Seed();
        await AddRow(TranslationStatus.Stale, deleted: true);

        Assert.Equal(ManualTranslationSaveResult.TranslationDeleted, await Save(await Translated()));

        await AssertNothingWritten(TranslationStatus.Stale);
    }

    private async Task AddPayload(string payload, bool deleted = false)
    {
        await using var context = _factory.CreateContext();
        context.ContentTranslations.Add(new ContentTranslation
        {
            ContentId = _contentId, CultureId = _cultureId, TranslationStatus = TranslationStatus.NeedsReview, SourceFingerprint = "old",
            LocalizedTextJson = payload, IsActive = true, IsDeleted = deleted
        });
        await context.SaveChangesAsync();
    }

    // Complete payload as LegacyFarsiContentParser.Serialize writes it; `with` tweaks it.
    private const string Complete = """
        {"title":"canonical","headLine":null,"abstract":null,"description":"<p>x</p>",
         "metadata":{"id":5,"title":null,"author":null,"keywords":null,"description":null},
         "sections":[{"id":1,"elements":[{"id":10,"tinyText":"t","editorText":null}]},{"id":2,"elements":[]}]}
        """;

    public static TheoryData<string, string> UnusablePayloads => new()
    {
        { "malformed", "{not json" },
        { "null payload", null },
        { "json null", "null" },
        { "array root", "[]" },
        { "incomplete (only title)", "{\"title\":\"x\"}" },
        { "missing content text", Complete.Replace("\"headLine\":null,", "") },
        { "missing metadata", Complete.Replace("\"metadata\":{\"id\":5,\"title\":null,\"author\":null,\"keywords\":null,\"description\":null},", "") },
        { "missing metadata text", Complete.Replace("\"author\":null,", "") },
        { "missing sections", Complete.Replace("\"sections\":", "\"sectionz\":") },
        { "missing elements", Complete.Replace(",\"elements\":[]", "") },
        { "missing element text", Complete.Replace(",\"editorText\":null", "") },
        { "missing element id", Complete.Replace("\"id\":10,", "") },
        { "null section id", Complete.Replace("\"id\":2,", "\"id\":null,") },
        { "duplicate section id", Complete.Replace("\"id\":2,", "\"id\":1,") },
        { "duplicate element id", Complete.Replace("{\"id\":2,\"elements\":[]}", "{\"id\":2,\"elements\":[{\"id\":10,\"tinyText\":null,\"editorText\":null}]}") },
        { "duplicate property", Complete.Replace("\"title\":\"canonical\",", "\"title\":\"canonical\",\"Title\":\"other\",") },
        { "number text", Complete.Replace("\"tinyText\":\"t\"", "\"tinyText\":1") },
        { "object text", Complete.Replace("\"title\":\"canonical\"", "\"title\":{}") },
        { "string id", Complete.Replace("\"id\":10", "\"id\":\"10\"") },
        { "fractional id", Complete.Replace("\"id\":10", "\"id\":10.5") },
        { "metadata array", Complete.Replace("{\"id\":5,\"title\":null,\"author\":null,\"keywords\":null,\"description\":null}", "[]") },
        { "sections object", Complete.Replace("\"sections\":[", "\"sections\":{\"a\":[").Replace("\"elements\":[]}]}", "\"elements\":[]}]}}") },
        { "null elements", Complete.Replace("\"elements\":[]", "\"elements\":null") },
        { "section not object", Complete.Replace("{\"id\":2,\"elements\":[]}", "2") },
    };

    // Unusable canonical text isn't a seed (legacy here is unusable too, so the source is).
    [Theory]
    [MemberData(nameof(UnusablePayloads))]
    public async Task GetEditor_UnusableCanonicalPayload_IsNotASeed(string _, string payload)
    {
        await Seed();
        await AddPayload(payload);

        var editor = await Editor();

        Assert.Equal((ManualTranslationSeed.Source, "English"), (editor.Seed, editor.Text.Title));
    }

    [Fact]
    public async Task GetEditor_CanonicalExplicitNulls_AreKept_UnmatchedNodesKeepSource()
    {
        await Seed();
        await AddPayload(Complete.Replace("\"title\":\"canonical\"", "\"title\":null"));

        var editor = await Editor();

        Assert.Equal((ManualTranslationSeed.Canonical, null, "<p>x</p>"), (editor.Seed, editor.Text.Title, editor.Text.Description));
        Assert.Equal("Meta", editor.Text.Metadata.Title); // payload's metadata id isn't master's
    }

    private async Task<ManualTranslationEditor> Editor(int applicationId = ApplicationId)
    {
        await using var context = _factory.CreateContext();
        return await Sut(context).GetEditor(_contentId, _cultureId, applicationId);
    }

    private async Task<(int MetadataId, int SectionId, int TinyId, int EditorId)> Ids()
    {
        await using var context = _factory.CreateContext();
        var master = await new ContentTranslationJobRepository(context).FindSourceGraph(_contentId);
        var section = master.Sections.Single(s => s.Priority == 2);
        return (master.Metadata.Id, section.Id, section.Elements.Single(e => e.ElementType == 1000).Id, section.Elements.Single(e => e.ElementType == 1002).Id);
    }

    private async Task SetLegacy(string legacy)
    {
        await using var context = _factory.CreateContext();
        (await context.Contents.SingleAsync(c => c.Id == _contentId)).FarsiContent = legacy;
        await context.SaveChangesAsync();
    }

    private static IEnumerable<LocalizedElementText> Elements(LocalizedContentText text) => text.Sections.SelectMany(s => s.Elements);

    [Fact]
    public async Task GetEditor_WithoutStoredText_SeedsFromSource_InMasterOrder()
    {
        await Seed(); // LegacyFarsi has no Id: unusable

        var editor = await Editor();

        Assert.Equal((ManualTranslationSeed.Source, (TranslationStatus?)null, await Fingerprint()), (editor.Seed, editor.TranslationStatus, editor.SourceFingerprint));
        Assert.Equal(("English", "Head", "Meta"), (editor.Text.Title, editor.Text.HeadLine, editor.Text.Metadata.Title));
        Assert.Empty(editor.Text.Sections[0].Elements); // priority 1 first
        Assert.Equal(new[] { "tiny", null }, Elements(editor.Text).Select(e => e.TinyText));
        Assert.Equal(LegacyFarsi, editor.Source.FarsiContent);
        await AssertNothingWritten(null);
    }

    // A snapshot from an older layout: its removed nodes are dropped, new master nodes keep the
    // source text, and matching text lands only on the same (section, element).
    [Fact]
    public async Task GetEditor_StaleLegacySnapshot_IsAlignedToCurrentMaster()
    {
        await Seed();
        var (metadataId, sectionId, tinyId, editorId) = await Ids();
        var legacy = $$"""
            {"Id":{{_contentId}},"Title":"FA","HeadLine":null,"FarsiContent":"nested","Metadata":{"Id":{{metadataId}},"Title":"FA meta"},
             "Sections":[{"Id":{{sectionId}},"Priority":9,"Elements":[{"Id":{{tinyId}},"TinyText":"FA tiny","FileNameText":"stale.pdf"},{"Id":999,"TinyText":"FA removed"}]},
                         {"Id":998,"Elements":[{"Id":{{editorId}},"EditorText":"<p>FA moved</p>"}]}]}
            """;
        await SetLegacy(legacy);

        var editor = await Editor();

        Assert.Equal(ManualTranslationSeed.Legacy, editor.Seed);
        Assert.Equal(("FA", null, "FA meta", null), (editor.Text.Title, editor.Text.HeadLine, editor.Text.Metadata.Title, editor.Text.Metadata.Author));
        Assert.Equal(new[] { tinyId, editorId }, Elements(editor.Text).Select(e => e.Id));
        Assert.Equal("FA tiny", Elements(editor.Text).Single(e => e.Id == tinyId).TinyText);
        Assert.Equal("<p>body</p>", Elements(editor.Text).Single(e => e.Id == editorId).EditorText);
        Assert.Equal(legacy, await Legacy());
        Assert.Null(await Row());
    }

    // A partial historical snapshot: omitted text inherits the current source, explicit null stays
    // an intentional blank - so saving the seeded form unchanged stores exactly that.
    [Fact]
    public async Task GetEditor_PartialLegacy_OmittedFieldsInheritSource_ExplicitNullStaysBlank_AndSurvivesUnchangedSave()
    {
        await Seed();
        var (metadataId, sectionId, tinyId, editorId) = await Ids();
        var legacy = $$"""
            {"Id":{{_contentId}},"Title":"FA","Description":null,
             "Metadata":{"Id":{{metadataId}},"Keywords":"FA kw","Author":null},
             "Sections":[{"Id":{{sectionId}},"Elements":[{"Id":{{tinyId}}},{"Id":{{editorId}},"TinyText":null,"EditorText":null}]}]}
            """;
        await SetLegacy(legacy);

        var editor = await Editor();

        Assert.Equal(ManualTranslationSeed.Legacy, editor.Seed);
        Assert.Equal(("FA", "Head", null, null), (editor.Text.Title, editor.Text.HeadLine, editor.Text.Abstract, editor.Text.Description));
        Assert.Equal(("Meta", null, "FA kw", null), (editor.Text.Metadata.Title, editor.Text.Metadata.Author, editor.Text.Metadata.Keywords, editor.Text.Metadata.Description));
        Assert.Equal(("tiny", (string)null), (Elements(editor.Text).Single(e => e.Id == tinyId).TinyText, Elements(editor.Text).Single(e => e.Id == tinyId).EditorText));
        Assert.Equal(((string)null, (string)null), (Elements(editor.Text).Single(e => e.Id == editorId).TinyText, Elements(editor.Text).Single(e => e.Id == editorId).EditorText));

        Assert.Equal(ManualTranslationSaveResult.Saved, await Save(editor.Text, expectedFingerprint: editor.SourceFingerprint));

        var stored = LegacyFarsiContentParser.Deserialize((await Row()).LocalizedTextJson);
        Assert.Equal(("FA", "Head", null, "Meta", null, "FA kw"),
            (stored.Title, stored.HeadLine, stored.Description, stored.Metadata.Title, stored.Metadata.Author, stored.Metadata.Keywords));
        Assert.Equal(new[] { ("tiny", (string)null), (null, null) }, stored.Sections.SelectMany(x => x.Elements).Select(e => (e.TinyText, e.EditorText)));
        Assert.Equal((TranslationStatus.Ready, legacy), ((await Row()).TranslationStatus, await Legacy()));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"Id":999,"Title":"other content"}""")]
    [InlineData("""{"Title":"no id"}""")]
    [InlineData("""{"Id":ID,"Title":1}""")]
    [InlineData("""{"Id":ID,"Metadata":[]}""")]
    [InlineData("   ")]
    public async Task GetEditor_UnusableLegacy_SeedsFromSource(string legacy)
    {
        await Seed();
        await SetLegacy(legacy.Replace("ID", _contentId.ToString()));

        var editor = await Editor();

        Assert.Equal((ManualTranslationSeed.Source, "English"), (editor.Seed, editor.Text.Title));
    }

    [Theory]
    [InlineData(TranslationStatus.NeedsReview, true, TranslationStatus.NeedsReview)]
    [InlineData(TranslationStatus.Ready, true, TranslationStatus.Ready)]
    [InlineData(TranslationStatus.Ready, false, TranslationStatus.Stale)]
    public async Task GetEditor_PrefersCanonicalOverLegacy_AndReportsItsStatus(TranslationStatus stored, bool currentSource, TranslationStatus expected)
    {
        await Seed();
        var (metadataId, sectionId, tinyId, _) = await Ids();
        await SetLegacy($$"""{"Id":{{_contentId}},"Title":"FA legacy"}""");
        var fingerprint = currentSource ? await Fingerprint() : "old";
        await using (var context = _factory.CreateContext())
        {
            context.ContentTranslations.Add(new ContentTranslation
            {
                ContentId = _contentId, CultureId = _cultureId, TranslationStatus = stored, SourceFingerprint = fingerprint, IsActive = true,
                LocalizedTextJson = LegacyFarsiContentParser.Serialize(new LocalizedContentText("canonical", null, null, null,
                    new LocalizedMetadataText(metadataId, "canon meta", null, null, null),
                    new List<LocalizedSectionText> { new(sectionId, new List<LocalizedElementText> { new(tinyId, "canon tiny", null) }) }))
            });
            await context.SaveChangesAsync();
        }

        var editor = await Editor();

        Assert.Equal((ManualTranslationSeed.Canonical, (TranslationStatus?)expected), (editor.Seed, editor.TranslationStatus));
        Assert.Equal(("canonical", "canon meta", "canon tiny"), (editor.Text.Title, editor.Text.Metadata.Title, Elements(editor.Text).Single(e => e.Id == tinyId).TinyText));
        Assert.Equal((stored, fingerprint), ((await Row()).TranslationStatus, (await Row()).SourceFingerprint));
    }

    [Fact]
    public async Task GetEditor_DeletedCanonical_IsIgnored()
    {
        await Seed();
        await SetLegacy($$"""{"Id":{{_contentId}},"Title":"FA legacy"}""");
        await AddPayload(Complete, deleted: true);

        var editor = await Editor();

        Assert.Equal((ManualTranslationSeed.Legacy, (TranslationStatus?)null, "FA legacy"), (editor.Seed, editor.TranslationStatus, editor.Text.Title));
    }

    [Fact]
    public async Task GetEditor_OtherApplicationsContent_IsNull()
    {
        await Seed();

        Assert.Null(await Editor(applicationId: 2));
    }

    [Fact]
    public void HasNoTranslationPortDependency()
    {
        var parameters = typeof(ManualContentTranslation).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);

        Assert.Equal(new[] { typeof(IContentTranslationJobRepository), typeof(TimeProvider) }, parameters);
    }
}
