using Application.CMSRepository;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Domains.Entities;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Application.UseCases.TranslatorServices.LegacyFarsiJobProgressState;

namespace Core.Tests.TranslatorServices;

// Over SQLite with the real candidate repository. No translation port or job processor is involved at all.
public class LegacyFarsiTranslationProgressTests : IDisposable
{
    private const int ApplicationId = 1;
    private const string LegacyFarsi = "{\"Title\":\"قدیمی\"}";
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private int _cultureId;
    private int _otherCultureId;
    private int _saves;
    private int _reads;

    public void Dispose() => _factory.Dispose();

    private async Task<int> Add(object entity)
    {
        await using var context = _factory.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync();
        return ((BaseEntity)entity).Id;
    }

    private async Task Seed()
    {
        _cultureId = await Add(new Culture { ApplicationId = ApplicationId, Title = "Farsi", Key = "fa-IR", IsActive = true });
        _otherCultureId = await Add(new Culture { ApplicationId = ApplicationId, Title = "Arabic", Key = "ar", IsActive = true });
    }

    private Task<int> AddContent(int applicationId = ApplicationId, bool isDeleted = false) =>
        Add(new Content { ApplicationId = applicationId, TypeId = 1001, Title = "English", FarsiContent = LegacyFarsi, IsDeleted = isDeleted, UpdatedDT = Now });

    private Task<int> AddJob(int contentId, ContentTranslationJobState state, int attempts = 0, string errorCode = null, int? cultureId = null,
        bool isDeleted = false) =>
        Add(new ContentTranslationJob
        {
            ContentId = contentId, CultureId = cultureId ?? _cultureId, SourceFingerprint = Guid.NewGuid().ToString("N").PadRight(64, '0'), State = state,
            AttemptCount = attempts, ErrorCode = errorCode, NextAttemptAt = Now, IsActive = true, IsDeleted = isDeleted
        });

    private async Task<IReadOnlyList<LegacyFarsiJobProgress>> Read(int[] ids, int? cultureId = null)
    {
        await using var context = _factory.CreateContext();
        context.SavingChanges += (_, _) => _saves++;
        var options = new ContentTranslationOptions { ActivationCultureId = cultureId ?? _cultureId, BulkRequestMaxItems = 6 };
        var repository = new CountingRepository(new LegacyFarsiTranslationCandidateRepository(context), () => _reads++);
        return await new LegacyFarsiTranslationProgress(repository, options).Read(ids, ApplicationId);
    }

    private static LegacyFarsiJobProgress Missing(int jobId) => new(jobId, null, NotFound, null, null);

    [Fact]
    public async Task ReturnsEveryLifecycleState_WithAttempts_AndOnlyFailedErrorCodes()
    {
        await Seed();
        var content = await AddContent();
        var queued = await AddJob(content, ContentTranslationJobState.Queued, attempts: 1, errorCode: "provider_timeout"); // stale code from a retry
        var processing = await AddJob(content, ContentTranslationJobState.Processing, attempts: 2, errorCode: "lease_expired");
        var succeeded = await AddJob(content, ContentTranslationJobState.Succeeded, attempts: 1, errorCode: "provider_error");
        var failed = await AddJob(content, ContentTranslationJobState.Failed, attempts: 3, errorCode: ContentTranslationErrorCodes.ProviderRetriesExhausted);
        var superseded = await AddJob(content, ContentTranslationJobState.Superseded, attempts: 1, errorCode: "invalid_output");

        var items = await Read([superseded, queued, processing, succeeded, failed]);

        Assert.Equal(
            [
                new(superseded, content, Superseded, 1, null), new(queued, content, Queued, 1, null), new(processing, content, Processing, 2, null),
                new(succeeded, content, Succeeded, 1, null), new(failed, content, Failed, 3, ContentTranslationErrorCodes.ProviderRetriesExhausted)
            ], items);
        Assert.Equal(0, _saves);
    }

    [Fact]
    public async Task DuplicatesCollapse_InFirstRequestedOrder()
    {
        await Seed();
        var content = await AddContent();
        var a = await AddJob(content, ContentTranslationJobState.Queued);
        var b = await AddJob(content, ContentTranslationJobState.Processing);

        var items = await Read([b, a, b, 999_999, a]);

        Assert.Equal([new(b, content, Processing, 0, null), new(a, content, Queued, 0, null), Missing(999_999)], items);
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6, 7 })] // over BulkRequestMaxItems
    [InlineData(new[] { 1, 1, 1, 1, 1, 1, 1 })] // duplicates count toward the limit
    [InlineData(new[] { 1, 0 })]
    [InlineData(new[] { 1, -2 })]
    public async Task InvalidRequest_IsRejectedBeforeAnyQuery(int[] ids)
    {
        await Seed();

        Assert.Null(await Read(ids));
        Assert.Null(await Read(null));
        Assert.Equal((0, 0), (_reads, _saves));
    }

    [Fact]
    public async Task ForeignDeletedOtherCultureAndMissingJobs_AreNotFound_WithoutData()
    {
        await Seed();
        var mine = await AddContent();
        var visible = await AddJob(mine, ContentTranslationJobState.Failed, attempts: 2, errorCode: "provider_error");
        var foreign = await AddJob(await AddContent(applicationId: 2), ContentTranslationJobState.Failed, attempts: 2, errorCode: "provider_error");
        var otherCulture = await AddJob(mine, ContentTranslationJobState.Succeeded, cultureId: _otherCultureId);
        var deletedJob = await AddJob(mine, ContentTranslationJobState.Queued, isDeleted: true);
        var deletedContent = await AddJob(await AddContent(isDeleted: true), ContentTranslationJobState.Queued);

        var items = await Read([foreign, otherCulture, deletedJob, deletedContent, 999_999, visible]);

        Assert.Equal(
            [Missing(foreign), Missing(otherCulture), Missing(deletedJob), Missing(deletedContent), Missing(999_999), new(visible, mine, Failed, 2, "provider_error")],
            items);
    }

    [Fact]
    public async Task UnsetCulture_ReportsNotFound_WithoutQuerying()
    {
        await Seed();
        var job = await AddJob(await AddContent(), ContentTranslationJobState.Queued);

        Assert.Equal([Missing(job)], await Read([job], cultureId: 0));
        Assert.Equal(0, _reads);
    }

    [Fact]
    public async Task Read_ChangesNoJobOrContent()
    {
        await Seed();
        var content = await AddContent();
        var job = await AddJob(content, ContentTranslationJobState.Processing, attempts: 1);
        await using var before = _factory.CreateContext();
        var jobBefore = await before.ContentTranslationJobs.AsNoTracking().SingleAsync(j => j.Id == job);
        var legacyBefore = System.Text.Encoding.UTF8.GetBytes(await before.Contents.Where(c => c.Id == content).Select(c => c.FarsiContent).SingleAsync());

        await Read([job]);
        await Read([job, job]);

        await using var after = _factory.CreateContext();
        var jobAfter = await after.ContentTranslationJobs.AsNoTracking().SingleAsync(j => j.Id == job);
        Assert.Equal((jobBefore.State, jobBefore.AttemptCount, jobBefore.Version, jobBefore.UpdatedDT), (jobAfter.State, jobAfter.AttemptCount, jobAfter.Version, jobAfter.UpdatedDT));
        Assert.Equal(legacyBefore, System.Text.Encoding.UTF8.GetBytes(await after.Contents.Where(c => c.Id == content).Select(c => c.FarsiContent).SingleAsync()));
        Assert.Single(await after.ContentTranslationJobs.ToListAsync());
        Assert.Empty(await after.ContentTranslations.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(0, _saves);
    }

    // Counts FindJobs calls; every other member is unused by the progress read.
    private sealed class CountingRepository(ILegacyFarsiTranslationCandidateRepository inner, Action onRead) : ILegacyFarsiTranslationCandidateRepository
    {
        public Task<List<TranslationJobSnapshot>> FindJobs(int applicationId, int cultureId, IReadOnlyCollection<int> jobIds, CancellationToken ct = default)
        {
            onRead();
            return inner.FindJobs(applicationId, cultureId, jobIds, ct);
        }

        public Task<bool> IsCultureAvailable(int cultureId, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<List<ActiveTranslationJob>> FindActiveJobs(LegacyFarsiCandidateFilter filter, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<List<Content>> FindSources(IReadOnlyCollection<int> contentIds, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<Dictionary<int, bool>> ClassifyOwned(LegacyFarsiCandidateFilter filter, IReadOnlyCollection<int> contentIds, CancellationToken ct = default) =>
            throw new InvalidOperationException();
        public Task<(int Total, List<LegacyFarsiTranslationCandidate> Items)> FindPage(LegacyFarsiCandidateFilter filter, IReadOnlyCollection<int> excludedContentIds,
            LegacyFarsiCandidateSort sort, bool descending, int page, int pageSize, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<(LegacyFarsiRecoveredJobCounts Counts, List<RecoveredTranslationJob> Items)> FindRecoveredJobs(int applicationId, int cultureId, IReadOnlyCollection<int> typeIds,
            DateTime completedSince, int page, int pageSize, CancellationToken ct = default) => throw new InvalidOperationException();
    }
}
