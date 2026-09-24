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
    private const string NewFarsi = "{\"Title\":\"manual\"}";
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

    // The editor's graph: a copy of the current master with translated text.
    private async Task<Content> Translated(Action<Content> edit = null)
    {
        await using var context = _factory.CreateContext();
        var graph = await new ContentTranslationJobRepository(context).FindSourceGraph(_contentId);
        graph.Title = "عنوان";
        graph.Description = "<p>شرح <strong>کامل</strong></p>";
        graph.Metadata.Author = "نویسنده"; // text the source doesn't have
        graph.HeadLine = null;             // left blank by the editor
        graph.Sections.Single(s => s.Priority == 2).Elements.Single(e => e.ElementType == 1002).EditorText = "<p>متن</p>";
        edit?.Invoke(graph);
        return graph;
    }

    private async Task<ManualTranslationSaveResult> Save(Content graph, int? cultureId = null, int applicationId = ApplicationId)
    {
        await using var context = _factory.CreateContext();
        var sut = new ManualContentTranslation(new ContentTranslationJobRepository(context), new ContentCommandRepository(context), new FakeTimeProvider(Now));
        return await sut.Save(_contentId, cultureId ?? _cultureId, applicationId, graph, NewFarsi);
    }

    private async Task<T> Read<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        await using var context = _factory.CreateContext();
        return await read(context);
    }

    private Task<string> Legacy() => Read(c => c.Contents.Where(x => x.Id == _contentId).Select(x => x.FarsiContent).SingleAsync());

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
    public async Task ValidSave_PersistsLegacyAndReadyTranslation_AndIsImmediatelyActivatable()
    {
        await Seed();
        await AddRow(TranslationStatus.Stale);

        Assert.Equal(ManualTranslationSaveResult.Saved, await Save(await Translated()));

        Assert.Equal(NewFarsi, await Legacy());
        var row = await Row();
        Assert.Equal((TranslationStatus.Ready, await Fingerprint(), ManualContentTranslation.Provider, null, Now, null),
            (row.TranslationStatus, row.SourceFingerprint, row.Provider, row.Model, row.TranslatedAt, row.Error));
        var text = LegacyFarsiContentParser.Deserialize(row.LocalizedTextJson);
        Assert.Equal(("عنوان", null, "<p>شرح <strong>کامل</strong></p>", "نویسنده"), (text.Title, text.HeadLine, text.Description, text.Metadata.Author));
        // Sections follow master order (priority), not the editor's.
        Assert.Equal(2, text.Sections.Count);
        Assert.Empty(text.Sections[0].Elements);
        Assert.Contains(text.Sections[1].Elements, e => e.EditorText == "<p>متن</p>");

        var state = await Read(c => new ContentTranslationRequests(new ContentTranslationJobRepository(c), new FakeTimeProvider(Now)).GetState(_contentId, _cultureId, ApplicationId));
        Assert.Equal(ContentTranslationState.Ready, state);
    }

    public static TheoryData<string, Action<Content>> InvalidEdits => new()
    {
        { "extra section (deleted since the editor loaded)", g => g.Sections.Add(new ContentSection { Id = 999, Elements = new List<SectionElement>() }) },
        { "missing section", g => g.Sections.Remove(g.Sections.First()) },
        { "missing element", g => g.Sections.Single(s => s.Priority == 2).Elements.Remove(g.Sections.Single(s => s.Priority == 2).Elements.First()) },
        { "element moved to another section", g =>
            {
                var from = g.Sections.Single(s => s.Priority == 2);
                var element = from.Elements.First();
                from.Elements.Remove(element);
                g.Sections.Single(s => s.Priority == 1).Elements.Add(element);
            } },
        { "wrong metadata id", g => g.Metadata.Id = 999 },
        { "missing metadata", g => g.Metadata = null },
        { "html structure changed", g => g.Description = "<p>شرح <em>کامل</em></p>" },
        { "malformed html", g => g.Description = "<p>شرح <strong>کامل</strong></p></div>" },
        { "other content", g => g.Id = 999 },
    };

    [Theory]
    [MemberData(nameof(InvalidEdits))]
    public async Task InvalidOrStaleStructure_IsRejected_AndPreservesBothPayloads(string _, Action<Content> edit)
    {
        await Seed();
        await AddRow(TranslationStatus.Stale);

        Assert.Equal(ManualTranslationSaveResult.InvalidStructure, await Save(await Translated(edit)));

        await AssertNothingWritten(TranslationStatus.Stale);
    }

    [Fact]
    public async Task SourceEditedAfterEditorLoaded_IsRejected()
    {
        await Seed();
        var graph = await Translated();
        await using (var context = _factory.CreateContext())
        {
            var section = await context.ContentSections.FirstAsync(s => s.ContentId == _contentId && s.Priority == 1);
            context.SectionElements.Add(new SectionElement { SectionId = section.Id, ElementType = 1000, TinyText = "new" });
            await context.SaveChangesAsync();
        }

        Assert.Equal(ManualTranslationSaveResult.InvalidStructure, await Save(graph));

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

    [Fact]
    public void HasNoTranslationPortDependency()
    {
        var parameters = typeof(ManualContentTranslation).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);

        Assert.Equal(new[] { typeof(IContentTranslationJobRepository), typeof(IContentCommandRepository), typeof(TimeProvider) }, parameters);
    }
}
