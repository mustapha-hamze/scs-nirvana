using Application.CMSRepository;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Domains.Entities;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Application.UseCases.TranslatorServices.LegacyFarsiBulkQueueOutcome;

namespace Core.Tests.TranslatorServices;

// Over SQLite with the real candidate and job repositories. No translation port is involved at all.
public class LegacyFarsiTranslationBulkQueueTests : IDisposable
{
    private const int ApplicationId = 1;
    private const string LegacyFarsi = "{\"Title\":\"قدیمی\"}";
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private int _cultureId;

    public void Dispose() => _factory.Dispose();

    private async Task<int> Add(object entity)
    {
        await using var context = _factory.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync();
        return ((BaseEntity)entity).Id;
    }

    private async Task SeedCulture(bool isActive = true) =>
        _cultureId = await Add(new Culture { ApplicationId = ApplicationId, Title = "Farsi", Key = "fa-IR", IsActive = isActive });

    private Task<int> AddContent(int typeId = 1001, string farsi = LegacyFarsi, int applicationId = ApplicationId, bool isDeleted = false) =>
        Add(new Content { ApplicationId = applicationId, TypeId = typeId, Title = "English", FarsiContent = farsi, IsDeleted = isDeleted, UpdatedDT = Now });

    private async Task<string> Fingerprint(int contentId)
    {
        await using var context = _factory.CreateContext();
        return ContentSourceFingerprint.Compute(await new ContentTranslationJobRepository(context).FindSourceGraph(contentId));
    }

    private async Task<int> AddJob(int contentId, ContentTranslationJobState state, string fingerprint = null) =>
        await Add(new ContentTranslationJob
        {
            ContentId = contentId, CultureId = _cultureId, SourceFingerprint = fingerprint ?? await Fingerprint(contentId), State = state,
            NextAttemptAt = Now, IsActive = true
        });

    private ContentTranslation Translation(int contentId, TranslationStatus status, string fingerprint = "fp", bool isDeleted = false) => new()
    {
        ContentId = contentId, CultureId = _cultureId, TranslationStatus = status, SourceFingerprint = fingerprint, LocalizedTextJson = "{}",
        IsActive = true, IsDeleted = isDeleted
    };

    private async Task<IReadOnlyList<LegacyFarsiBulkQueueItem>> Queue(int[] ids, int? cultureId = null, int applicationId = ApplicationId,
        Func<ApplicationDbContext, IContentTranslationJobRepository> jobRepository = null)
    {
        await using var context = _factory.CreateContext();
        var options = new ContentTranslationOptions { ActivationCultureId = cultureId ?? _cultureId, LegacyBulkCandidateTypeIds = [1001, 1002], BulkRequestMaxItems = 3 };
        var requests = new ContentTranslationRequests(jobRepository?.Invoke(context) ?? new ContentTranslationJobRepository(context), new FakeTimeProvider(Now));
        return await new LegacyFarsiTranslationBulkQueue(new LegacyFarsiTranslationCandidateRepository(context), requests, options)
            .Queue(ids, applicationId);
    }

    private async Task<List<ContentTranslationJob>> Jobs()
    {
        await using var context = _factory.CreateContext();
        return await context.ContentTranslationJobs.ToListAsync();
    }

    private async Task<int> JobId(int contentId) => (await Jobs()).Single(j => j.ContentId == contentId).Id;

    [Fact]
    public async Task QueuesEachDistinctId_InFirstRequestedOrder_AndIsIdempotent()
    {
        await SeedCulture();
        var a = await AddContent();
        var b = await AddContent(typeId: 1002);

        var first = await Queue([b, a, b]);
        var second = await Queue([a, b]);

        Assert.Equal([new(b, Queued, await JobId(b)), new(a, Queued, await JobId(a))], first);
        Assert.Equal([new(a, AlreadyQueued, await JobId(a)), new(b, AlreadyQueued, await JobId(b))], second);
        Assert.Equal(2, (await Jobs()).Count);
        Assert.All(await Jobs(), j => Assert.Equal(ContentTranslationPrecondition.NoTranslation, j.TranslationPrecondition));
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1, 2, 3, 4 })] // over BulkRequestMaxItems
    [InlineData(new[] { 1, 1, 1, 1 })] // duplicates count toward the limit
    [InlineData(new[] { 1, 0 })]
    [InlineData(new[] { 1, -2 })]
    public async Task InvalidRequest_IsRejectedWhole_AndQueuesNothing(int[] ids)
    {
        await SeedCulture();
        await AddContent();

        Assert.Null(await Queue(ids));
        Assert.Null(await Queue(null));
        Assert.Empty(await Jobs());
    }

    [Fact]
    public async Task RevalidatesEveryIdAtSubmission()
    {
        await SeedCulture();
        var candidate = await AddContent();
        var foreign = await AddContent(applicationId: 2);
        var deleted = await AddContent(isDeleted: true);
        var disallowedType = await AddContent(typeId: 2000);
        var blankFarsi = await AddContent(farsi: " \t\r\n");
        var noFarsi = await AddContent(farsi: null);
        var translated = await AddContent();
        await Add(Translation(translated, TranslationStatus.Failed));
        var deletedTranslation = await AddContent();
        await Add(Translation(deletedTranslation, TranslationStatus.Ready, isDeleted: true));

        var items = new List<LegacyFarsiBulkQueueItem>();
        foreach (var chunk in new[] { candidate, foreign, deleted, disallowedType, blankFarsi, noFarsi, translated, deletedTranslation, 999_999 }.Chunk(3))
            items.AddRange(await Queue(chunk));

        Assert.Equal(
            [
                new(candidate, Queued, await JobId(candidate)), new(foreign, NotFound, null), new(deleted, NotFound, null),
                new(disallowedType, Skipped, null), new(blankFarsi, Skipped, null), new(noFarsi, Skipped, null),
                new(translated, Skipped, null), new(deletedTranslation, Skipped, null), new(999_999, NotFound, null)
            ], items);
        Assert.Equal([candidate], (await Jobs()).Select(j => j.ContentId));
    }

    [Fact]
    public async Task ExistingActiveJob_IsAlreadyQueued_TerminalJobIsRetried_OldFingerprintJobIsIgnored()
    {
        await SeedCulture();
        var processing = await AddContent();
        var processingJob = await AddJob(processing, ContentTranslationJobState.Processing);
        var failed = await AddContent();
        var failedJob = await AddJob(failed, ContentTranslationJobState.Failed);
        var oldSource = await AddContent();
        await AddJob(oldSource, ContentTranslationJobState.Queued, fingerprint: new string('0', 64));

        var items = await Queue([processing, failed, oldSource]);

        Assert.Equal([new(processing, AlreadyQueued, processingJob), new(failed, Queued, failedJob), new(oldSource, Queued, (await Jobs()).Max(j => j.Id))], items);
        Assert.Equal(4, (await Jobs()).Count);
        Assert.Equal(ContentTranslationPrecondition.NoTranslation, (await Jobs()).Single(j => j.Id == failedJob).TranslationPrecondition);
        Assert.Equal(ContentTranslationPrecondition.None, (await Jobs()).Single(j => j.Id == processingJob).TranslationPrecondition);
    }

    [Theory]
    [InlineData("unset")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    public async Task UnavailableCulture_ReturnsCultureUnavailable_AndQueuesNothing(string scenario)
    {
        await SeedCulture(isActive: scenario != "inactive");
        if (scenario == "deleted")
        {
            await using var context = _factory.CreateContext();
            await context.Cultures.IgnoreQueryFilters().ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true));
        }
        var a = await AddContent();
        var foreign = await AddContent(applicationId: 2);

        var items = await Queue([a, foreign, a], cultureId: scenario == "unset" ? 0 : null);

        Assert.Equal([new(a, CultureUnavailable, null), new(foreign, CultureUnavailable, null)], items);
        Assert.Empty(await Jobs());
    }

    [Theory]
    [InlineData("concurrent-job", AlreadyQueued)]
    [InlineData("ready-translation", AlreadyReady)]
    [InlineData("content-deleted", NotFound)]
    public async Task RaceAfterRevalidation_IsReportedWithoutASecondJob(string race, LegacyFarsiBulkQueueOutcome expected)
    {
        await SeedCulture();
        var id = await AddContent();
        var fingerprint = await Fingerprint(id);
        Func<Task> action = race switch
        {
            "concurrent-job" => () => AddJob(id, ContentTranslationJobState.Queued, fingerprint),
            "ready-translation" => () => Add(Translation(id, TranslationStatus.Ready, fingerprint)),
            _ => async () =>
            {
                await using var context = _factory.CreateContext();
                await context.Contents.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true));
            }
        };

        // A concurrent request's insert lands just before this one saves; the others land before Request loads.
        var item = Assert.Single(await Queue([id], jobRepository: context => new RacingRepository(context, action, onSave: race == "concurrent-job")));

        var jobs = await Jobs();
        Assert.Equal(new LegacyFarsiBulkQueueItem(id, expected, expected == AlreadyQueued ? jobs.Single().Id : null), item);
        Assert.True(jobs.Count <= 1);
    }

    [Theory]
    [InlineData(TranslationStatus.Ready, false)] // for an older source
    [InlineData(TranslationStatus.Stale, false)]
    [InlineData(TranslationStatus.Failed, false)]
    [InlineData(TranslationStatus.NeedsReview, false)]
    [InlineData(TranslationStatus.Ready, true)]
    public async Task TranslationWrittenAfterClassification_BeforeQueueing_IsSkipped_WithNoJob(TranslationStatus status, bool deleted)
    {
        await SeedCulture();
        var id = await AddContent();

        var items = await Queue([id], jobRepository: context =>
            new RacingRepository(context, () => Add(Translation(id, status, isDeleted: deleted)), onSave: false));

        Assert.Equal([new(id, Skipped, null)], items);
        Assert.Empty(await Jobs());
    }

    // The row lands after Request's own check but before its job is saved: the job is queued, and
    // the worker must fail it without calling the provider or touching the row.
    [Fact]
    public async Task TranslationWrittenWhileQueueing_WorkerFailsTheJob_WithoutProviderCall_OrOverwrite()
    {
        await SeedCulture();
        var id = await AddContent();
        var items = await Queue([id], jobRepository: context =>
            new RacingRepository(context, () => Add(Translation(id, TranslationStatus.NeedsReview)), onSave: true));
        var jobId = Assert.Single(await Jobs()).Id;
        Assert.Equal([new(id, Queued, jobId)], items);
        var before = await RowSnapshot(id);

        await using (var context = _factory.CreateContext())
        {
            var port = new NeverCalledPort();
            await new ContentTranslationJobProcessor(new ContentTranslationJobRepository(context), port, new ContentTranslationOptions(), new FakeTimeProvider(Now))
                .RunOnce("worker", default);
            Assert.Equal(0, port.Calls);
        }

        var job = Assert.Single(await Jobs());
        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.TranslationConflict), (job.State, job.ErrorCode));
        Assert.Equal(before, await RowSnapshot(id));
    }

    private sealed class NeverCalledPort : ITranslationPort
    {
        public int Calls;

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(TranslationResult.Ok("{}", "p", "m"));
        }
    }

    private async Task<string> RowSnapshot(int contentId)
    {
        await using var context = _factory.CreateContext();
        return System.Text.Json.JsonSerializer.Serialize(
            await context.ContentTranslations.IgnoreQueryFilters().AsNoTracking().SingleAsync(t => t.ContentId == contentId));
    }

    [Fact]
    public async Task LegacyFarsiBytes_AreUnchanged()
    {
        await SeedCulture();
        var id = await AddContent();
        await using var before = _factory.CreateContext();
        var bytes = System.Text.Encoding.UTF8.GetBytes(await before.Contents.Where(c => c.Id == id).Select(c => c.FarsiContent).SingleAsync());

        await Queue([id]);
        await Queue([id]);

        await using var after = _factory.CreateContext();
        Assert.Equal(bytes, System.Text.Encoding.UTF8.GetBytes(await after.Contents.Where(c => c.Id == id).Select(c => c.FarsiContent).SingleAsync()));
    }

    // Runs the race once after the bulk queue's revalidation: before Request loads its state, or
    // before its first save.
    private sealed class RacingRepository(ApplicationDbContext context, Func<Task> race, bool onSave) : IContentTranslationJobRepository
    {
        private readonly ContentTranslationJobRepository _inner = new(context);
        private bool _raced;

        private async Task Race(bool saving)
        {
            if (!_raced && saving == onSave)
            {
                _raced = true;
                await race();
            }
        }

        public async Task<bool> ContentBelongsToApplication(int contentId, int applicationId, CancellationToken ct = default)
        {
            await Race(saving: false);
            return await _inner.ContentBelongsToApplication(contentId, applicationId, ct);
        }

        public async Task<bool> TrySaveChanges(CancellationToken ct = default)
        {
            await Race(saving: true);
            return await _inner.TrySaveChanges(ct);
        }

        public Task<ContentTranslationJob> FindNextClaimable(DateTime utcNow, CancellationToken ct = default) => _inner.FindNextClaimable(utcNow, ct);
        public Task<Culture> FindCulture(int cultureId, CancellationToken ct = default) => _inner.FindCulture(cultureId, ct);
        public Task<Content> FindSourceGraph(int contentId, CancellationToken ct = default) => _inner.FindSourceGraph(contentId, ct);
        public Task<ContentTranslation> FindTranslation(int contentId, int cultureId, CancellationToken ct = default) => _inner.FindTranslation(contentId, cultureId, ct);
        public void AddTranslation(ContentTranslation translation) => _inner.AddTranslation(translation);
        public Task<ContentTranslationJob> FindJob(int contentId, int cultureId, string fingerprint, CancellationToken ct = default) => _inner.FindJob(contentId, cultureId, fingerprint, ct);
        public void AddJob(ContentTranslationJob job) => _inner.AddJob(job);
    }
}
