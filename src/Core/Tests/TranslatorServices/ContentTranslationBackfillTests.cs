using Application.CMSRepository;
using Application.UnitOfWork;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace Core.Tests.TranslatorServices;

public class LegacyFarsiContentParserTests
{
    private static Content Master() => new()
    {
        Id = 10, TypeId = 1000, Title = "Title",
        Metadata = new ContentMetadata { Id = 20, ContentId = 10, Title = "Meta" },
        Sections = new List<ContentSection>
        {
            new() { Id = 30, ContentId = 10, Elements = new List<SectionElement> { new() { Id = 40, SectionId = 30 }, new() { Id = 41, SectionId = 30 } } },
            new() { Id = 31, ContentId = 10, Elements = new List<SectionElement>() }
        }
    };

    [Fact]
    public void ValidLegacySnapshot_ExtractsOnlyLocalizedText_CaseInsensitively()
    {
        const string legacy = """
            {"id":10,"TITLE":"عنوان","headline":"سرتیتر","Abstract":null,"Description":"<p>شرح</p>","FarsiContent":null,
             "Categories":"1,2","Images":[{"Id":1,"ImageFileName":"a.jpg"}],"IsActive":true,
             "metadata":{"Id":20,"ContentId":10,"Title":"متا","Author":"نویسنده","Keywords":"کلید","Description":"توضیح","UpdatedDT":"2020-01-01"},
             "SECTIONS":[{"Id":30,"ContentId":10,"Priority":1,"elements":[
                {"Id":40,"SectionId":30,"ElementType":1000,"TinyText":"کوتاه","EditorText":null,"FileNameText":"f.pdf","GalleryImages":"g","ElementTitle":"t"}]}]}
            """;

        var result = LegacyFarsiContentParser.Parse(legacy, Master());

        Assert.Null(result.ReasonCode);
        var text = LegacyFarsiContentParser.Deserialize(result.LocalizedTextJson);
        Assert.Equal(("عنوان", "سرتیتر", null, "<p>شرح</p>"), (text.Title, text.HeadLine, text.Abstract, text.Description));
        Assert.Equal(new LocalizedMetadataText(20, "متا", "نویسنده", "کلید", "توضیح"), text.Metadata);
        var section = Assert.Single(text.Sections);
        Assert.Equal(30, section.Id);
        Assert.Equal(new LocalizedElementText(40, "کوتاه", null), Assert.Single(section.Elements));

        // Only the whitelisted text survives; Farsi stays readable, HTML stays escaped.
        foreach (var excluded in new[] { "image", "fileName", "gallery", "elementTitle", "categories", "isActive", "priority", "updated", "farsi", "a.jpg", "<p>" })
            Assert.DoesNotContain(excluded, result.LocalizedTextJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("عنوان", result.LocalizedTextJson);
    }

    [Fact]
    public void MissingMetadataAndSections_AreUntranslatedNotRejected()
    {
        var result = LegacyFarsiContentParser.Parse("""{"Id":10,"Title":"x","Metadata":null}""", Master());

        var text = LegacyFarsiContentParser.Deserialize(result.LocalizedTextJson);
        Assert.Null(text.Metadata);
        Assert.Empty(text.Sections);
    }

    [Theory]
    [InlineData("{not json", LegacyFarsiContentParser.InvalidJson)]
    [InlineData("[]", LegacyFarsiContentParser.RootNotObject)]
    [InlineData("\"text\"", LegacyFarsiContentParser.RootNotObject)]
    [InlineData("""{"Title":"x"}""", LegacyFarsiContentParser.MissingId)]
    [InlineData("""{"Id":"10"}""", LegacyFarsiContentParser.InvalidId)]
    [InlineData("""{"Id":11}""", LegacyFarsiContentParser.IdMismatch)]
    [InlineData("""{"Id":10,"id":10}""", LegacyFarsiContentParser.DuplicateProperty)]
    [InlineData("""{"Id":10,"Title":"a","title":"b"}""", LegacyFarsiContentParser.DuplicateProperty)]
    [InlineData("""{"Id":10,"Title":5}""", LegacyFarsiContentParser.InvalidText)]
    [InlineData("""{"Id":10,"Metadata":[]}""", LegacyFarsiContentParser.InvalidShape)]
    [InlineData("""{"Id":10,"Metadata":{"Id":21}}""", LegacyFarsiContentParser.IdMismatch)]
    [InlineData("""{"Id":10,"Metadata":{"Id":20,"ContentId":11}}""", LegacyFarsiContentParser.IdMismatch)]
    [InlineData("""{"Id":10,"Sections":{}}""", LegacyFarsiContentParser.InvalidShape)]
    [InlineData("""{"Id":10,"Sections":[1]}""", LegacyFarsiContentParser.InvalidShape)]
    [InlineData("""{"Id":10,"Sections":[{"Priority":1}]}""", LegacyFarsiContentParser.MissingId)]
    [InlineData("""{"Id":10,"Sections":[{"Id":30},{"Id":30}]}""", LegacyFarsiContentParser.DuplicateId)]
    [InlineData("""{"Id":10,"Sections":[{"Id":99}]}""", LegacyFarsiContentParser.IdMismatch)]
    [InlineData("""{"Id":10,"Sections":[{"Id":31,"Elements":[{"Id":40}]}]}""", LegacyFarsiContentParser.IdMismatch)]
    [InlineData("""{"Id":10,"Sections":[{"Id":30,"Elements":[{"Id":40,"SectionId":31}]}]}""", LegacyFarsiContentParser.IdMismatch)]
    [InlineData("""{"Id":10,"Sections":[{"Id":30,"Elements":[{"Id":40},{"Id":40}]}]}""", LegacyFarsiContentParser.DuplicateId)]
    [InlineData("""{"Id":10,"Sections":[{"Id":30,"Elements":[{"Id":1.5}]}]}""", LegacyFarsiContentParser.InvalidId)]
    public void MalformedOrAmbiguous_IsRejectedWithReasonOnly(string legacy, string reason)
    {
        var result = LegacyFarsiContentParser.Parse(legacy, Master());

        Assert.Equal(reason, result.ReasonCode);
        Assert.Null(result.LocalizedTextJson);
    }
}

// End-to-end over SQLite with the real repository and UnitOfWork.
public class ContentTranslationBackfillTests : IDisposable
{
    private const string RunKey = "fa-backfill-1";
    private readonly SqliteContextFactory _factory = new();
    private int _cultureId;

    public void Dispose() => _factory.Dispose();

    private async Task<int> SeedCulture(bool deleted = false)
    {
        await using var context = _factory.CreateContext();
        var culture = new Culture { ApplicationId = 1, Title = "Farsi", Key = "fa-IR", IsDeleted = deleted };
        context.Cultures.Add(culture);
        await context.SaveChangesAsync();
        return culture.Id;
    }

    // farsi: null -> no FarsiContent; "valid" -> a real legacy snapshot of the saved graph with
    // Farsi text (serialized exactly like FarsiContentMapper); anything else is stored verbatim.
    private async Task<int> SeedContent(string farsi, bool deleted = false)
    {
        await using var context = _factory.CreateContext();
        var content = new Content
        {
            ApplicationId = 1, TypeId = 1000, Title = "Title", HeadLine = "Head", IsActive = true, IsDeleted = deleted,
            Metadata = new ContentMetadata { Title = "Meta", Author = "Author" },
            Sections = new List<ContentSection>
            {
                new() { Priority = 1, Elements = new List<SectionElement> { new() { ElementType = 1000, TinyText = "tiny", FileNameText = "f.pdf" } } }
            }
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        if (farsi == "valid")
        {
            content.Title = "عنوان";
            content.Sections.Single().Elements.Single().TinyText = "کوتاه";
            farsi = JsonConvert.SerializeObject(content, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            await using var reset = _factory.CreateContext();
            await reset.Contents.Where(c => c.Id == content.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.FarsiContent, farsi));
        }
        else
        {
            await context.Contents.Where(c => c.Id == content.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.FarsiContent, farsi));
        }

        return content.Id;
    }

    private static ContentTranslationBackfill CreateSut(ApplicationDbContext context, IContentTranslationBackfillRepository repository = null) =>
        new(repository ?? new ContentTranslationBackfillRepository(context),
            new Infrastructure.UnitOfWork.UnitOfWork(context, NullLogger<Infrastructure.UnitOfWork.UnitOfWork>.Instance));

    private async Task<ContentTranslationBackfillBatchResult> Run(int batchSize, string runKey = RunKey, int? cultureId = null)
    {
        await using var context = _factory.CreateContext();
        return await CreateSut(context).RunBatch(cultureId ?? _cultureId, runKey, batchSize);
    }

    private async Task<T> Read<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        await using var context = _factory.CreateContext();
        return await read(context);
    }

    [Fact]
    public async Task Batches_ImportConservatively_ResumeFromCheckpoint_AndStayIdempotent()
    {
        _cultureId = await SeedCulture();
        var valid = await SeedContent("valid");
        var malformed = await SeedContent("{\"Id\": 1");
        await SeedContent(null);
        await SeedContent("   ");
        await SeedContent("valid", deleted: true);
        var existing = await SeedContent("valid");
        var legacyBefore = await Read(c => c.Contents.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => x.FarsiContent).ToListAsync());
        await using (var context = _factory.CreateContext())
        {
            context.ContentTranslations.Add(new ContentTranslation
            {
                ContentId = existing, CultureId = _cultureId, TranslationStatus = TranslationStatus.Ready,
                SourceFingerprint = "manual", LocalizedTextJson = "{}", Provider = "manual", IsDeleted = true
            });
            await context.SaveChangesAsync();
        }

        // Blank, null and soft-deleted contents are never scanned.
        Assert.Equal(new ContentTranslationBackfillBatchResult(2, 1, 1, 0, malformed, false), await Run(2));
        Assert.Equal(new ContentTranslationBackfillBatchResult(1, 0, 0, 1, existing, true), await Run(2));
        Assert.Equal(new ContentTranslationBackfillBatchResult(0, 0, 0, 0, existing, true), await Run(2));

        var translations = await Read(c => c.ContentTranslations.IgnoreQueryFilters().ToDictionaryAsync(t => t.ContentId));
        Assert.Equal(3, translations.Count);

        var imported = translations[valid];
        var source = await Read(c => new ContentTranslationRepository(c).GetSourceGraph(valid));
        Assert.Equal(TranslationStatus.Stale, imported.TranslationStatus);
        Assert.Equal(ContentSourceFingerprint.Compute(source), imported.SourceFingerprint);
        Assert.Equal(ContentTranslationBackfill.LegacyProvider, imported.Provider);
        Assert.Null(imported.Model);
        Assert.Null(imported.TranslatedAt);
        Assert.Null(imported.Error);
        var text = LegacyFarsiContentParser.Deserialize(imported.LocalizedTextJson);
        Assert.Equal("عنوان", text.Title);
        Assert.Equal("کوتاه", text.Sections.Single().Elements.Single().TinyText);

        var review = translations[malformed];
        Assert.Equal(TranslationStatus.NeedsReview, review.TranslationStatus);
        Assert.Null(review.LocalizedTextJson);
        Assert.Equal(LegacyFarsiContentParser.InvalidJson, review.Error);
        Assert.Null(review.TranslatedAt);

        // The pre-existing (even soft-deleted) row is never overwritten.
        var kept = translations[existing];
        Assert.Equal((TranslationStatus.Ready, "manual", "{}", true), (kept.TranslationStatus, kept.SourceFingerprint, kept.LocalizedTextJson, kept.IsDeleted));

        Assert.Equal(legacyBefore, await Read(c => c.Contents.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => x.FarsiContent).ToListAsync()));

        var checkpoint = await Read(c => c.ContentTranslationBackfillCheckpoints.SingleAsync());
        Assert.Equal((RunKey, _cultureId, existing, 3, 1, 1, 1, 3),
            (checkpoint.RunKey, checkpoint.CultureId, checkpoint.LastContentId, checkpoint.ScannedCount, checkpoint.ImportedStaleCount,
                checkpoint.NeedsReviewCount, checkpoint.SkippedExistingCount, checkpoint.Version));
    }

    [Fact]
    public async Task ExistingTranslation_UnderNewRunKey_IsSkippedNotDuplicated()
    {
        _cultureId = await SeedCulture();
        var contentId = await SeedContent("valid");
        await Run(10);
        var before = await Read(c => c.ContentTranslations.SingleAsync());

        Assert.Equal(new ContentTranslationBackfillBatchResult(1, 0, 0, 1, contentId, true), await Run(10, runKey: "second-run"));

        var after = await Read(c => c.ContentTranslations.SingleAsync());
        Assert.Equal((before.Id, before.LocalizedTextJson, before.UpdatedDT), (after.Id, after.LocalizedTextJson, after.UpdatedDT));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task BatchSize_OutOfBounds_Throws(int batchSize)
    {
        _cultureId = await SeedCulture();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Run(batchSize));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDeletedCulture_ThrowsBeforeAnyWrite(bool seedDeleted)
    {
        var cultureId = seedDeleted ? await SeedCulture(deleted: true) : 999;
        await SeedContent("valid");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(10, cultureId: cultureId));

        Assert.False(await Read(c => c.ContentTranslationBackfillCheckpoints.IgnoreQueryFilters().AnyAsync()));
        Assert.False(await Read(c => c.ContentTranslations.IgnoreQueryFilters().AnyAsync()));
    }

    [Fact]
    public async Task RunKey_ReusedForAnotherCulture_Throws()
    {
        _cultureId = await SeedCulture();
        var other = await SeedCulture();
        await Run(10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(10, cultureId: other));
    }

    [Fact]
    public async Task ConcurrentCheckpointAdvance_RollsBackWholeBatch()
    {
        _cultureId = await SeedCulture();
        var first = await SeedContent("valid");
        await SeedContent("valid");
        await Run(1);

        await using (var context = _factory.CreateContext())
        {
            // Another runner commits a batch for the same runKey while this one is mid-batch.
            var racing = new RacingRepository(context);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => CreateSut(context, racing).RunBatch(_cultureId, RunKey, 1));
        }

        Assert.Equal(first, (await Read(c => c.ContentTranslations.SingleAsync())).ContentId);
        var checkpoint = await Read(c => c.ContentTranslationBackfillCheckpoints.SingleAsync());
        Assert.Equal((first, 1, 1), (checkpoint.LastContentId, checkpoint.ScannedCount, checkpoint.Version));
    }

    [Fact]
    public void HasNoTranslatorDependency()
    {
        var parameters = typeof(ContentTranslationBackfill).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);

        Assert.Equal(new[] { typeof(IContentTranslationBackfillRepository), typeof(IUnitOfWork) }, parameters);
    }

    [Fact]
    public void CheckpointModel_MatchesDbaContract()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused").Options;
        using var context = new ApplicationDbContext(options, TimeProvider.System);
        var entity = context.Model.FindEntityType(typeof(ContentTranslationBackfillCheckpoint));

        Assert.Equal("CMS_ContentTranslationBackfillCheckpoints", entity.GetTableName());
        Assert.Equal("varchar(100)", entity.FindProperty(nameof(ContentTranslationBackfillCheckpoint.RunKey)).GetColumnType());
        Assert.True(entity.FindProperty(nameof(ContentTranslationBackfillCheckpoint.Version)).IsConcurrencyToken);
        Assert.True(entity.GetIndexes().Single(i => i.Properties.Single().Name == nameof(ContentTranslationBackfillCheckpoint.RunKey)).IsUnique);
    }

    private sealed class RacingRepository(ApplicationDbContext context) : IContentTranslationBackfillRepository
    {
        private readonly ContentTranslationBackfillRepository _inner = new(context);

        public Task<bool> CultureExists(int cultureId, CancellationToken ct = default) => _inner.CultureExists(cultureId, ct);
        public Task<ContentTranslationBackfillCheckpoint> GetCheckpoint(string runKey, CancellationToken ct = default) => _inner.GetCheckpoint(runKey, ct);
        public void AddCheckpoint(ContentTranslationBackfillCheckpoint checkpoint) => _inner.AddCheckpoint(checkpoint);
        public void AddTranslation(ContentTranslation translation) => _inner.AddTranslation(translation);
        public Task<List<int>> GetTranslatedContentIds(IReadOnlyCollection<int> ids, int cultureId, CancellationToken ct = default) => _inner.GetTranslatedContentIds(ids, cultureId, ct);

        public async Task<List<Content>> GetLegacyFarsiBatch(int afterContentId, int take, CancellationToken ct = default)
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE CMS_ContentTranslationBackfillCheckpoints SET Version = Version + 1", ct);
            return await _inner.GetLegacyFarsiBatch(afterContentId, take, ct);
        }
    }
}
