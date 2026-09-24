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

// Over SQLite with the real job repository. No translation port is involved at all.
public class ContentTranslationRequestsTests : IDisposable
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
        var content = new Content { ApplicationId = ApplicationId, TypeId = 1000, Title = "English", FarsiContent = LegacyFarsi };
        context.AddRange(culture, content);
        await context.SaveChangesAsync();
        (_contentId, _cultureId) = (content.Id, culture.Id);
    }

    private async Task<string> Fingerprint()
    {
        await using var context = _factory.CreateContext();
        return ContentSourceFingerprint.Compute(await new ContentTranslationJobRepository(context).FindSourceGraph(_contentId));
    }

    private async Task Add(object entity)
    {
        await using var context = _factory.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync();
    }

    private async Task<T> Run<T>(Func<ContentTranslationRequests, Task<T>> action, Func<ApplicationDbContext, IContentTranslationJobRepository> repository = null)
    {
        await using var context = _factory.CreateContext();
        return await action(new ContentTranslationRequests(repository?.Invoke(context) ?? new ContentTranslationJobRepository(context), new FakeTimeProvider(Now)));
    }

    private Task<ContentTranslationRequestResult> Request(int? applicationId = null) =>
        Run(sut => sut.Request(_contentId, _cultureId, applicationId ?? ApplicationId));

    private Task<ContentTranslationState?> State() => Run(sut => sut.GetState(_contentId, _cultureId, ApplicationId));

    private async Task<List<ContentTranslationJob>> Jobs()
    {
        await using var context = _factory.CreateContext();
        return await context.ContentTranslationJobs.ToListAsync();
    }

    private async Task<string> Legacy()
    {
        await using var context = _factory.CreateContext();
        return await context.Contents.Where(c => c.Id == _contentId).Select(c => c.FarsiContent).SingleAsync();
    }

    private ContentTranslation Translation(TranslationStatus status, string fingerprint) => new()
    {
        ContentId = _contentId, CultureId = _cultureId, TranslationStatus = status, SourceFingerprint = fingerprint,
        LocalizedTextJson = "{}", Provider = "test", IsActive = true
    };

    [Fact]
    public async Task RepeatedRequests_QueueExactlyOneJob()
    {
        await Seed();

        var first = await Request();
        var second = await Request();

        Assert.Equal(ContentTranslationState.Queued, first.State);
        Assert.Equal(first, second);
        var job = Assert.Single(await Jobs());
        Assert.Equal((first.JobId, await Fingerprint(), ContentTranslationJobState.Queued, 0, Now),
            ((int?)job.Id, job.SourceFingerprint, job.State, job.AttemptCount, job.NextAttemptAt));
        Assert.Equal(ContentTranslationState.Queued, await State());
        Assert.Equal(LegacyFarsi, await Legacy());
    }

    [Fact]
    public async Task MatchingReadyTranslation_IsReturned_WithoutJobOrOverwrite()
    {
        await Seed();
        var ready = Translation(TranslationStatus.Ready, await Fingerprint());
        await Add(ready);

        Assert.Equal(new ContentTranslationRequestResult(ContentTranslationState.Ready, null), await Request());
        Assert.Equal(ContentTranslationState.Ready, await State());
        Assert.Empty(await Jobs());
    }

    [Fact]
    public async Task ProcessingJob_IsReturnedUnchanged()
    {
        await Seed();
        var job = new ContentTranslationJob
        {
            ContentId = _contentId, CultureId = _cultureId, SourceFingerprint = await Fingerprint(), State = ContentTranslationJobState.Processing,
            AttemptCount = 1, LeaseOwner = "w", LeaseExpiresAt = Now.AddMinutes(5), Version = 3, IsActive = true
        };
        await Add(job);

        Assert.Equal(new ContentTranslationRequestResult(ContentTranslationState.Processing, job.Id), await Request());
        var stored = Assert.Single(await Jobs());
        Assert.Equal((ContentTranslationJobState.Processing, 3, "w"), (stored.State, stored.Version, stored.LeaseOwner));
    }

    [Theory]
    [InlineData(ContentTranslationJobState.Failed)]
    [InlineData(ContentTranslationJobState.Superseded)] // source reverted to an earlier fingerprint
    public async Task TerminalJob_IsRequeuedInPlace_AsExplicitRetry(ContentTranslationJobState state)
    {
        await Seed();
        var job = new ContentTranslationJob
        {
            ContentId = _contentId, CultureId = _cultureId, SourceFingerprint = await Fingerprint(), State = state, AttemptCount = 5,
            ErrorCode = ContentTranslationErrorCodes.ProviderTimeout, CompletedAt = Now.AddHours(-1), NextAttemptAt = Now.AddHours(-2), IsActive = true
        };
        await Add(job);

        Assert.Equal(new ContentTranslationRequestResult(ContentTranslationState.Queued, job.Id), await Request());
        var stored = Assert.Single(await Jobs());
        Assert.Equal((ContentTranslationJobState.Queued, 0, null, null, Now, 1),
            (stored.State, stored.AttemptCount, stored.ErrorCode, stored.CompletedAt, stored.NextAttemptAt, stored.Version));
    }

    [Fact]
    public async Task StaleTranslation_QueuesJobForCurrentFingerprint_AndLeavesRowUntouched()
    {
        await Seed();
        await Add(Translation(TranslationStatus.Ready, new string('0', 64)));
        Assert.Equal(ContentTranslationState.Stale, await State());

        var result = await Request();

        Assert.Equal(ContentTranslationState.Queued, result.State);
        await using var context = _factory.CreateContext();
        var row = await context.ContentTranslations.SingleAsync();
        Assert.Equal((TranslationStatus.Ready, new string('0', 64)), (row.TranslationStatus, row.SourceFingerprint));
    }

    [Fact]
    public async Task SourceChange_QueuesNewJob_ForNewFingerprint()
    {
        await Seed();
        await Request();
        await using (var context = _factory.CreateContext())
            await context.Contents.Where(c => c.Id == _contentId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Title, "Edited"));

        await Request();

        Assert.Equal(2, (await Jobs()).Select(j => j.SourceFingerprint).Distinct().Count());
    }

    [Fact]
    public async Task ConcurrentInsert_ReturnsTheWinnersJob()
    {
        await Seed();
        var fingerprint = await Fingerprint();
        var winner = new ContentTranslationJob
        {
            ContentId = _contentId, CultureId = _cultureId, SourceFingerprint = fingerprint, State = ContentTranslationJobState.Queued,
            NextAttemptAt = Now, IsActive = true
        };

        var result = await Run(sut => sut.Request(_contentId, _cultureId, ApplicationId),
            context => new RacingRepository(context, () => Add(winner)));

        Assert.Equal(new ContentTranslationRequestResult(ContentTranslationState.Queued, winner.Id), result);
        Assert.Single(await Jobs());
    }

    [Theory]
    [InlineData("foreign-app")]
    [InlineData("deleted-content")]
    public async Task ContentNotOwned_ReturnsNull_AndQueuesNothing(string scenario)
    {
        await Seed();
        if (scenario == "deleted-content")
        {
            await using var context = _factory.CreateContext();
            await context.Contents.Where(c => c.Id == _contentId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true));
        }
        var applicationId = scenario == "foreign-app" ? 2 : ApplicationId;

        Assert.Null(await Request(applicationId));
        Assert.Null(await Run(sut => sut.GetState(_contentId, _cultureId, applicationId)));
        Assert.Empty(await Jobs());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDeletedCulture_IsUnavailable_AndQueuesNothing(bool deleted)
    {
        await Seed(cultureDeleted: deleted);
        if (!deleted)
            _cultureId = 999;

        Assert.Equal(new ContentTranslationRequestResult(ContentTranslationState.CultureUnavailable, null), await Request());
        Assert.Equal(ContentTranslationState.CultureUnavailable, await State());
        Assert.Null(await Run(sut => sut.GetState(_contentId, _cultureId, 2))); // ownership is checked first
        Assert.Empty(await Jobs());
    }

    [Theory]
    [InlineData(null, "current", ContentTranslationState.Missing)] // non-empty legacy FarsiContent is ignored
    [InlineData(TranslationStatus.NeedsReview, "old", ContentTranslationState.NeedsReview)]
    [InlineData(TranslationStatus.Stale, "current", ContentTranslationState.Stale)]
    [InlineData(TranslationStatus.Failed, "current", ContentTranslationState.Failed)]
    public async Task GetState_ReflectsTranslationRow(TranslationStatus? status, string fingerprint, ContentTranslationState expected)
    {
        await Seed();
        if (status != null)
            await Add(Translation(status.Value, fingerprint == "current" ? await Fingerprint() : new string('0', 64)));

        Assert.Equal(expected, await State());
    }

    [Fact]
    public async Task GetState_FailedJobForCurrentSource_IsFailed()
    {
        await Seed();
        await Add(new ContentTranslationJob
        {
            ContentId = _contentId, CultureId = _cultureId, SourceFingerprint = await Fingerprint(), State = ContentTranslationJobState.Failed, IsActive = true
        });

        Assert.Equal(ContentTranslationState.Failed, await State());
    }

    // A concurrent request inserts the same (content, culture, fingerprint) job just before save.
    private sealed class RacingRepository(ApplicationDbContext context, Func<Task> race) : IContentTranslationJobRepository
    {
        private readonly ContentTranslationJobRepository _inner = new(context);
        private bool _raced;

        public async Task<bool> TrySaveChanges(CancellationToken ct = default)
        {
            if (!_raced)
            {
                _raced = true;
                await race();
            }
            return await _inner.TrySaveChanges(ct);
        }

        public Task<ContentTranslationJob> FindNextClaimable(DateTime utcNow, CancellationToken ct = default) => _inner.FindNextClaimable(utcNow, ct);
        public Task<bool> ContentBelongsToApplication(int contentId, int applicationId, CancellationToken ct = default) => _inner.ContentBelongsToApplication(contentId, applicationId, ct);
        public Task<Culture> FindCulture(int cultureId, CancellationToken ct = default) => _inner.FindCulture(cultureId, ct);
        public Task<Content> FindSourceGraph(int contentId, CancellationToken ct = default) => _inner.FindSourceGraph(contentId, ct);
        public Task<ContentTranslation> FindTranslation(int contentId, int cultureId, CancellationToken ct = default) => _inner.FindTranslation(contentId, cultureId, ct);
        public void AddTranslation(ContentTranslation translation) => _inner.AddTranslation(translation);
        public Task<ContentTranslationJob> FindJob(int contentId, int cultureId, string fingerprint, CancellationToken ct = default) => _inner.FindJob(contentId, cultureId, fingerprint, ct);
        public void AddJob(ContentTranslationJob job) => _inner.AddJob(job);
    }
}
