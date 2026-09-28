using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// ErrorCode (a fixed ContentTranslationErrorCodes value) only for Failed. RelevantAt (UTC) is the last
// update of an active job or the completion of a terminal one.
public record LegacyFarsiRecoveredJob(int JobId, int ContentId, string Title, int TypeId, bool IsActive, LegacyFarsiJobProgressState State,
    int AttemptCount, string ErrorCode, DateTime RelevantAt);

// CultureAvailable is false when ContentTranslation:ActivationCultureId is unset, deleted or inactive.
public record LegacyFarsiRecoveredJobPage(bool CultureAvailable, IReadOnlyList<LegacyFarsiRecoveredJob> Items, int TotalCount, int Page, int PageSize);

// Read-only recovery of the queue dashboard after a refresh: every Queued/Processing job and every job
// completed within LegacyBulkRecentJobDays, for the configured activation culture on the application's
// non-deleted content of a LegacyBulkCandidateTypeIds type. There is no batch or requester on a job, so
// this is the application's operational view (single-content requests of those types included), not
// one user's batch. Candidate eligibility is deliberately not applied: a job stays visible after it
// succeeds or its source or legacy Farsi changes. Never writes, queues, fingerprints or calls the provider.
public class LegacyFarsiTranslationRecoveredJobs
{
    public const int MaxPage = int.MaxValue / LegacyFarsiTranslationCandidates.MaxPageSize;

    private readonly ILegacyFarsiTranslationCandidateRepository _repository;
    private readonly ContentTranslationOptions _options;
    private readonly TimeProvider _timeProvider;

    public LegacyFarsiTranslationRecoveredJobs(ILegacyFarsiTranslationCandidateRepository repository, ContentTranslationOptions options,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _options = options;
        _timeProvider = timeProvider;
    }

    // PageSize is clamped to 1..MaxPageSize, as for candidates; Page to 1..MaxPage, so the offset never overflows.
    public async Task<LegacyFarsiRecoveredJobPage> Find(int page, int pageSize, int applicationId, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, MaxPage);
        pageSize = Math.Clamp(pageSize, 1, LegacyFarsiTranslationCandidates.MaxPageSize);

        var cultureId = _options.ActivationCultureId;
        if (cultureId <= 0 || !await _repository.IsCultureAvailable(cultureId, cancellationToken))
            return new LegacyFarsiRecoveredJobPage(false, [], 0, page, pageSize);
        if (_options.LegacyBulkCandidateTypeIds is not { Length: > 0 } typeIds)
            return new LegacyFarsiRecoveredJobPage(true, [], 0, page, pageSize);

        var completedSince = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-_options.LegacyBulkRecentJobDays);
        var (total, jobs) = await _repository.FindRecoveredJobs(applicationId, cultureId, typeIds, completedSince, page, pageSize, cancellationToken);
        var items = jobs.Select(j => new LegacyFarsiRecoveredJob(j.JobId, j.ContentId, j.Title, j.TypeId, j.IsActive, LegacyFarsiTranslationProgress.ToState(j.State),
            j.AttemptCount, j.State == ContentTranslationJobState.Failed ? j.ErrorCode : null, j.RelevantAt)).ToList();
        return new LegacyFarsiRecoveredJobPage(true, items, total, page, pageSize);
    }
}
