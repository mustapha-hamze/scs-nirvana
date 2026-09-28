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
public class LegacyFarsiTranslationRecoveredJobsTests : IDisposable
{
    private const int ApplicationId = 1;
    private const int TypeId = 1001;
    private const string LegacyFarsi = "{\"Title\":\"قدیمی\"}";
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cutoff = Now.AddDays(-7);

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

    private async Task Seed(bool cultureActive = true)
    {
        _cultureId = await Add(new Culture { ApplicationId = ApplicationId, Title = "Farsi", Key = "fa-IR", IsActive = cultureActive });
        _otherCultureId = await Add(new Culture { ApplicationId = ApplicationId, Title = "Arabic", Key = "ar", IsActive = true });
    }

    private Task<int> AddContent(string title = "English", int applicationId = ApplicationId, int typeId = TypeId, string farsi = LegacyFarsi,
        bool isActive = true, bool isDeleted = false) =>
        Add(new Content
        {
            ApplicationId = applicationId, TypeId = typeId, Title = title, FarsiContent = farsi, IsActive = isActive, IsDeleted = isDeleted, UpdatedDT = Now
        });

    private Task<int> AddJob(int contentId, ContentTranslationJobState state, DateTime? completedAt = null, DateTime? updatedAt = null, int attempts = 1,
        string errorCode = null, int? cultureId = null, bool isDeleted = false) =>
        Add(new ContentTranslationJob
        {
            ContentId = contentId, CultureId = cultureId ?? _cultureId, SourceFingerprint = Guid.NewGuid().ToString("N").PadRight(64, '0'), State = state,
            AttemptCount = attempts, ErrorCode = errorCode, NextAttemptAt = Now, CompletedAt = completedAt, UpdatedDT = updatedAt ?? Now.AddHours(-1),
            LeaseOwner = "secret-lease", IsActive = true, IsDeleted = isDeleted
        });

    private async Task<LegacyFarsiRecoveredJobPage> Find(int page = 1, int pageSize = 25, int? cultureId = null, int[] typeIds = null)
    {
        await using var context = _factory.CreateContext();
        context.SavingChanges += (_, _) => _saves++;
        var options = new ContentTranslationOptions { ActivationCultureId = cultureId ?? _cultureId, LegacyBulkCandidateTypeIds = typeIds ?? [TypeId, 1002] };
        var repository = new CountingRepository(new LegacyFarsiTranslationCandidateRepository(context), () => _reads++);
        var result = await new LegacyFarsiTranslationRecoveredJobs(repository, options, new FakeTimeProvider(Now)).Find(page, pageSize, ApplicationId);
        Assert.Empty(context.ChangeTracker.Entries());
        return result;
    }

    private async Task<int[]> JobIds(int page = 1, int pageSize = 25) => (await Find(page, pageSize)).Items.Select(i => i.JobId).ToArray();

    [Fact]
    public async Task OnlyTheApplicationsConfiguredCultureAndTypeJobs_OnNonDeletedContent()
    {
        await Seed();
        var mine = await AddContent();
        var visible = await AddJob(mine, ContentTranslationJobState.Queued);
        await AddJob(await AddContent(applicationId: 2), ContentTranslationJobState.Queued);
        await AddJob(mine, ContentTranslationJobState.Queued, cultureId: _otherCultureId);
        await AddJob(await AddContent(typeId: 2000), ContentTranslationJobState.Processing);
        await AddJob(mine, ContentTranslationJobState.Queued, isDeleted: true);
        await AddJob(await AddContent(isDeleted: true), ContentTranslationJobState.Queued);

        var page = await Find();

        Assert.Equal([visible], page.Items.Select(i => i.JobId));
        Assert.Equal((true, 1, 1, 25), (page.CultureAvailable, page.TotalCount, page.Page, page.PageSize));
    }

    [Fact]
    public async Task ActiveJobsAreAlwaysListed_TerminalOnlyWithinTheRetentionWindow()
    {
        await Seed();
        var content = await AddContent();
        var oldQueued = await AddJob(content, ContentTranslationJobState.Queued, updatedAt: Now.AddDays(-90), completedAt: Now.AddDays(-60));
        var oldProcessing = await AddJob(content, ContentTranslationJobState.Processing, updatedAt: Now.AddDays(-45));
        var atCutoff = await AddJob(content, ContentTranslationJobState.Succeeded, completedAt: Cutoff);
        var failed = await AddJob(content, ContentTranslationJobState.Failed, completedAt: Now.AddDays(-1));
        var superseded = await AddJob(content, ContentTranslationJobState.Superseded, completedAt: Now.AddDays(-2));
        foreach (var state in new[] { ContentTranslationJobState.Succeeded, ContentTranslationJobState.Failed, ContentTranslationJobState.Superseded })
            await AddJob(content, state, completedAt: Cutoff.AddTicks(-1));
        await AddJob(content, ContentTranslationJobState.Failed, completedAt: null); // terminal without a completion time

        Assert.Equal([oldProcessing, oldQueued, failed, superseded, atCutoff], await JobIds());
    }

    [Fact]
    public async Task RetentionFollowsTheConfiguredDays()
    {
        await Seed();
        var content = await AddContent();
        var recent = await AddJob(content, ContentTranslationJobState.Succeeded, completedAt: Now.AddDays(-1));
        await AddJob(content, ContentTranslationJobState.Succeeded, completedAt: Now.AddDays(-2));

        await using var context = _factory.CreateContext();
        var options = new ContentTranslationOptions { ActivationCultureId = _cultureId, LegacyBulkCandidateTypeIds = [TypeId], LegacyBulkRecentJobDays = 1 };
        var page = await new LegacyFarsiTranslationRecoveredJobs(new LegacyFarsiTranslationCandidateRepository(context), options, new FakeTimeProvider(Now))
            .Find(1, 25, ApplicationId);

        Assert.Equal([recent], page.Items.Select(i => i.JobId));
    }

    [Fact]
    public async Task CandidateEligibilityNeverHidesAnExistingJob()
    {
        await Seed();
        var translated = await AddContent("Translated");
        var blankFarsi = await AddContent("Blank", farsi: "  ");
        var noFarsi = await AddContent("None", farsi: null);
        var translatedJob = await AddJob(translated, ContentTranslationJobState.Succeeded, completedAt: Now.AddMinutes(-1));
        var blankJob = await AddJob(blankFarsi, ContentTranslationJobState.Queued); // its fingerprint matches nothing current
        var noFarsiJob = await AddJob(noFarsi, ContentTranslationJobState.Superseded, completedAt: Now.AddMinutes(-2));
        await Add(new ContentTranslation
        {
            ContentId = translated, CultureId = _cultureId, TranslationStatus = TranslationStatus.Ready, SourceFingerprint = new string('a', 64),
            LocalizedTextJson = "{}", IsActive = true
        });

        Assert.Equal([blankJob, translatedJob, noFarsiJob], await JobIds());
    }

    [Fact]
    public async Task OrderedActiveFirst_ThenNewestRelevantTime_ThenId_AndPaged()
    {
        await Seed();
        var content = await AddContent();
        var tieTime = Now.AddHours(-3);
        var queuedOld = await AddJob(content, ContentTranslationJobState.Queued, updatedAt: Now.AddHours(-5));
        var tieA = await AddJob(content, ContentTranslationJobState.Processing, updatedAt: tieTime);
        var tieB = await AddJob(content, ContentTranslationJobState.Queued, updatedAt: tieTime);
        var newestDone = await AddJob(content, ContentTranslationJobState.Succeeded, completedAt: Now.AddMinutes(-1), updatedAt: Now.AddDays(-3));
        var olderDone = await AddJob(content, ContentTranslationJobState.Failed, completedAt: Now.AddHours(-1), updatedAt: Now);

        Assert.Equal([tieB, tieA, queuedOld, newestDone, olderDone], await JobIds());
        var second = await Find(page: 2, pageSize: 2);
        Assert.Equal([queuedOld, newestDone], second.Items.Select(i => i.JobId));
        Assert.Equal((5, 2, 2), (second.TotalCount, second.Page, second.PageSize));
        Assert.Empty((await Find(page: 4, pageSize: 2)).Items);
    }

    [Fact]
    public async Task Counts_CoverEveryPage_ByState_WithTheRowsPredicate()
    {
        await Seed();
        var content = await AddContent();
        await AddJob(content, ContentTranslationJobState.Queued, updatedAt: Now.AddDays(-90)); // active: any age
        await AddJob(content, ContentTranslationJobState.Queued);
        await AddJob(content, ContentTranslationJobState.Processing, updatedAt: Now.AddDays(-45));
        await AddJob(content, ContentTranslationJobState.Succeeded, completedAt: Cutoff); // exactly at the cutoff
        await AddJob(content, ContentTranslationJobState.Succeeded, completedAt: Now.AddHours(-1));
        await AddJob(content, ContentTranslationJobState.Failed, completedAt: Now.AddDays(-1), errorCode: "provider_error");
        await AddJob(content, ContentTranslationJobState.Superseded, completedAt: Now.AddDays(-2));
        // Outside the scope: past the cutoff, no completion time, other application, culture, type, deleted job or content.
        await AddJob(content, ContentTranslationJobState.Succeeded, completedAt: Cutoff.AddTicks(-1));
        await AddJob(content, ContentTranslationJobState.Failed, completedAt: null);
        await AddJob(await AddContent(applicationId: 2), ContentTranslationJobState.Queued);
        await AddJob(content, ContentTranslationJobState.Queued, cultureId: _otherCultureId);
        await AddJob(await AddContent(typeId: 2000), ContentTranslationJobState.Processing);
        await AddJob(content, ContentTranslationJobState.Queued, isDeleted: true);
        await AddJob(await AddContent(isDeleted: true), ContentTranslationJobState.Queued);

        var first = await Find(page: 1, pageSize: 2);
        var all = await Find(page: 1, pageSize: 100);

        Assert.Equal(new LegacyFarsiRecoveredJobCounts(2, 1, 2, 1, 1), first.Counts);
        Assert.Equal((3, 7, 7), (first.Counts.Active, first.Counts.Total, first.TotalCount));
        Assert.Equal(first.Counts, all.Counts);
        Assert.Equal(all.Counts.Total, all.Items.Count); // the rows and the counts share one predicate
        Assert.Equal(2, _reads); // one repository read per page, however many states
        Assert.Equal(["Queued", "Processing", "Succeeded", "Failed", "Superseded", "Active", "Total"],
            typeof(LegacyFarsiRecoveredJobCounts).GetProperties().Select(p => p.Name));
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 1000, 1, LegacyFarsiTranslationCandidates.MaxPageSize)]
    [InlineData(int.MaxValue, 25, LegacyFarsiTranslationRecoveredJobs.MaxPage, 25)]
    public async Task PagingIsBounded(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        await Seed();
        await AddJob(await AddContent(), ContentTranslationJobState.Queued);

        var result = await Find(page, pageSize);

        Assert.Equal((expectedPage, expectedPageSize, 1), (result.Page, result.PageSize, result.TotalCount));
    }

    [Fact]
    public async Task ReturnsOnlyTheSafeFields_WithFailureReasonsForFailedOnly()
    {
        await Seed();
        var content = await AddContent("<b>Title</b>", isActive: false);
        var failed = await AddJob(content, ContentTranslationJobState.Failed, completedAt: Now.AddHours(-2), attempts: 5,
            errorCode: ContentTranslationErrorCodes.ProviderRetriesExhausted);
        var queued = await AddJob(content, ContentTranslationJobState.Queued, updatedAt: Now.AddHours(-4), attempts: 1, errorCode: "provider_timeout");
        var succeeded = await AddJob(content, ContentTranslationJobState.Succeeded, completedAt: Now.AddHours(-3), errorCode: "provider_error");
        var unknown = await AddJob(content, ContentTranslationJobState.Failed, completedAt: Now.AddHours(-5), errorCode: "secret-unknown-code");

        var items = (await Find()).Items;

        Assert.Equal(
            [
                new(queued, content, "<b>Title</b>", TypeId, false, Queued, 1, null, Now.AddHours(-4)),
                new(failed, content, "<b>Title</b>", TypeId, false, Failed, 5, ContentTranslationErrorCodes.FailureReason(ContentTranslationErrorCodes.ProviderRetriesExhausted),
                    Now.AddHours(-2)),
                new LegacyFarsiRecoveredJob(succeeded, content, "<b>Title</b>", TypeId, false, Succeeded, 1, null, Now.AddHours(-3)),
                new(unknown, content, "<b>Title</b>", TypeId, false, Failed, 1, ContentTranslationErrorCodes.GenericFailureReason, Now.AddHours(-5))
            ], items);
        Assert.Equal(
            ["JobId", "ContentId", "Title", "TypeId", "IsActive", "State", "AttemptCount", "FailureReason", "RelevantAt"],
            typeof(LegacyFarsiRecoveredJob).GetProperties().Select(p => p.Name));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(999_999, true)]
    [InlineData(null, false)] // the configured culture exists but is inactive
    public async Task UnavailableCulture_IsAnEmptyPage_WithoutQueryingJobs(int? cultureId, bool cultureActive)
    {
        await Seed(cultureActive);
        await AddJob(await AddContent(), ContentTranslationJobState.Queued);

        var page = await Find(cultureId: cultureId);

        Assert.Equal((false, 0), (page.CultureAvailable, page.TotalCount));
        Assert.Equal(LegacyFarsiRecoveredJobCounts.None, page.Counts);
        Assert.Empty(page.Items);
        Assert.Equal((0, 0), (_reads, _saves));
    }

    [Fact]
    public async Task NoConfiguredTypes_IsAnEmptyPage_WithoutQueryingJobs()
    {
        await Seed();
        await AddJob(await AddContent(), ContentTranslationJobState.Queued);

        var page = await Find(typeIds: []);

        Assert.True(page.CultureAvailable);
        Assert.Empty(page.Items);
        Assert.Equal(0, _reads);
    }

    [Fact]
    public async Task Find_ChangesNothing()
    {
        await Seed();
        var content = await AddContent();
        await AddJob(content, ContentTranslationJobState.Processing);
        await AddJob(content, ContentTranslationJobState.Failed, completedAt: Now.AddHours(-1), errorCode: "provider_error");

        async Task<string> Snapshot()
        {
            await using var context = _factory.CreateContext();
            return string.Join("|",
                string.Join(",", await context.Contents.IgnoreQueryFilters().OrderBy(c => c.Id).Select(c => $"{c.Id}:{c.FarsiContent}:{c.UpdatedDT:O}").ToListAsync()),
                string.Join(",", await context.ContentTranslationJobs.IgnoreQueryFilters().OrderBy(j => j.Id)
                    .Select(j => $"{j.Id}:{j.State}:{j.Version}:{j.AttemptCount}:{j.UpdatedDT:O}:{j.LeaseOwner}").ToListAsync()),
                await context.ContentTranslations.IgnoreQueryFilters().CountAsync());
        }

        var before = await Snapshot();
        Assert.Equal(2, (await Find()).TotalCount);
        Assert.Equal(before, await Snapshot());
        Assert.Equal(0, _saves);
    }

    [Fact]
    public void HasNoTranslationPortDependency()
    {
        var parameters = typeof(LegacyFarsiTranslationRecoveredJobs).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);

        Assert.Equal(new[] { typeof(ILegacyFarsiTranslationCandidateRepository), typeof(ContentTranslationOptions), typeof(TimeProvider) }, parameters);
    }

    // Counts FindRecoveredJobs calls; the culture check passes through, and nothing else may be used.
    private sealed class CountingRepository(ILegacyFarsiTranslationCandidateRepository inner, Action onRead) : ILegacyFarsiTranslationCandidateRepository
    {
        public Task<(LegacyFarsiRecoveredJobCounts Counts, List<RecoveredTranslationJob> Items)> FindRecoveredJobs(int applicationId, int cultureId, IReadOnlyCollection<int> typeIds,
            DateTime completedSince, int page, int pageSize, CancellationToken ct = default)
        {
            onRead();
            return inner.FindRecoveredJobs(applicationId, cultureId, typeIds, completedSince, page, pageSize, ct);
        }

        public Task<bool> IsCultureAvailable(int cultureId, CancellationToken ct = default) => inner.IsCultureAvailable(cultureId, ct);
        public Task<List<TranslationJobSnapshot>> FindJobs(int applicationId, int cultureId, IReadOnlyCollection<int> jobIds, CancellationToken ct = default) =>
            throw new InvalidOperationException();
        public Task<List<ActiveTranslationJob>> FindActiveJobs(LegacyFarsiCandidateFilter filter, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<List<Content>> FindSources(IReadOnlyCollection<int> contentIds, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<Dictionary<int, bool>> ClassifyOwned(LegacyFarsiCandidateFilter filter, IReadOnlyCollection<int> contentIds, CancellationToken ct = default) =>
            throw new InvalidOperationException();
        public Task<(int Total, List<LegacyFarsiTranslationCandidate> Items)> FindPage(LegacyFarsiCandidateFilter filter, IReadOnlyCollection<int> excludedContentIds,
            LegacyFarsiCandidateSort sort, bool descending, int page, int pageSize, CancellationToken ct = default) => throw new InvalidOperationException();
    }
}
