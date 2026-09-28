using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

public enum LegacyFarsiJobProgressState
{
    Queued,
    Processing,
    Succeeded,
    Failed,
    Superseded,
    NotFound
}

// ContentId and AttemptCount are null for NotFound; ErrorCode (a fixed ContentTranslationErrorCodes
// value) only for Failed.
public record LegacyFarsiJobProgress(int JobId, int? ContentId, LegacyFarsiJobProgressState State, int? AttemptCount, string ErrorCode);

// Read-only state of translation jobs a bulk queue request returned. A job is visible only when it is
// not deleted, is for the configured activation culture and is on the application's non-deleted
// content; anything else is NotFound. Never writes, queues, fingerprints or calls the provider.
public class LegacyFarsiTranslationProgress
{
    private readonly ILegacyFarsiTranslationCandidateRepository _repository;
    private readonly ContentTranslationOptions _options;

    public LegacyFarsiTranslationProgress(ILegacyFarsiTranslationCandidateRepository repository, ContentTranslationOptions options)
    {
        _repository = repository;
        _options = options;
    }

    // null (and nothing read) when the request is empty, has more than BulkRequestMaxItems IDs
    // (duplicates included) or a non-positive ID. Otherwise one item per distinct ID, in first-requested order.
    public async Task<IReadOnlyList<LegacyFarsiJobProgress>> Read(IReadOnlyList<int> jobIds, int applicationId,
        CancellationToken cancellationToken = default)
    {
        if (jobIds is not { Count: > 0 } || jobIds.Count > _options.BulkRequestMaxItems || jobIds.Any(id => id <= 0))
            return null;
        var ids = jobIds.Distinct().ToList();

        var cultureId = _options.ActivationCultureId;
        var found = cultureId <= 0
            ? new Dictionary<int, TranslationJobSnapshot>()
            : (await _repository.FindJobs(applicationId, cultureId, ids, cancellationToken)).ToDictionary(j => j.JobId);

        return ids.Select(id => found.TryGetValue(id, out var job)
            ? new LegacyFarsiJobProgress(id, job.ContentId, ToState(job.State), job.AttemptCount,
                job.State == ContentTranslationJobState.Failed ? job.ErrorCode : null)
            : new LegacyFarsiJobProgress(id, null, LegacyFarsiJobProgressState.NotFound, null, null)).ToList();
    }

    private static LegacyFarsiJobProgressState ToState(ContentTranslationJobState state) => state switch
    {
        ContentTranslationJobState.Queued => LegacyFarsiJobProgressState.Queued,
        ContentTranslationJobState.Processing => LegacyFarsiJobProgressState.Processing,
        ContentTranslationJobState.Succeeded => LegacyFarsiJobProgressState.Succeeded,
        ContentTranslationJobState.Failed => LegacyFarsiJobProgressState.Failed,
        ContentTranslationJobState.Superseded => LegacyFarsiJobProgressState.Superseded,
        _ => LegacyFarsiJobProgressState.NotFound
    };
}
