using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

public enum LegacyFarsiBulkQueueOutcome
{
    Queued,
    AlreadyQueued,
    AlreadyReady,
    Skipped,
    CultureUnavailable,
    NotFound
}

// JobId only for Queued and AlreadyQueued, for a later progress request.
public record LegacyFarsiBulkQueueItem(int ContentId, LegacyFarsiBulkQueueOutcome Outcome, int? JobId);

// Queues background translation of a bounded set of selected legacy-Farsi candidates into the
// configured activation culture. Every ID is re-checked against the candidate rules at submission
// time; only one that still passes goes through ContentTranslationRequests.Request, which owns the
// fingerprint, job and idempotency logic. Never calls the provider and never touches FarsiContent.
public class LegacyFarsiTranslationBulkQueue
{
    private readonly ILegacyFarsiTranslationCandidateRepository _repository;
    private readonly ContentTranslationRequests _requests;
    private readonly ContentTranslationOptions _options;

    public LegacyFarsiTranslationBulkQueue(ILegacyFarsiTranslationCandidateRepository repository, ContentTranslationRequests requests,
        ContentTranslationOptions options)
    {
        _repository = repository;
        _requests = requests;
        _options = options;
    }

    // null (and nothing queued) when the request is empty, has more than BulkRequestMaxItems IDs
    // (duplicates included) or a non-positive ID. Otherwise one item per distinct ID, in first-requested order.
    public async Task<IReadOnlyList<LegacyFarsiBulkQueueItem>> Queue(IReadOnlyList<int> contentIds, int applicationId,
        CancellationToken cancellationToken = default)
    {
        if (contentIds is not { Count: > 0 } || contentIds.Count > _options.BulkRequestMaxItems || contentIds.Any(id => id <= 0))
            return null;
        var seen = new HashSet<int>();
        var ids = contentIds.Where(seen.Add).ToList();

        var cultureId = _options.ActivationCultureId;
        if (cultureId <= 0 || !await _repository.IsCultureAvailable(cultureId, cancellationToken))
            return ids.Select(id => new LegacyFarsiBulkQueueItem(id, LegacyFarsiBulkQueueOutcome.CultureUnavailable, null)).ToList();

        // Rule 5 (no active job for the current fingerprint) is Request's own idempotency check,
        // which reports an existing job as AlreadyQueued instead of queueing a second one. Rule 4
        // is re-checked by Request (NoTranslation) and then by the worker, so a translation row
        // written after this check is never overwritten by a job queued here.
        var filter = new LegacyFarsiCandidateFilter(applicationId, cultureId, _options.LegacyBulkCandidateTypeIds, null, null);
        var owned = await _repository.ClassifyOwned(filter, ids, cancellationToken);

        var items = new List<LegacyFarsiBulkQueueItem>(ids.Count);
        foreach (var id in ids)
        {
            if (!owned.TryGetValue(id, out var isCandidate))
                items.Add(new LegacyFarsiBulkQueueItem(id, LegacyFarsiBulkQueueOutcome.NotFound, null));
            else if (!isCandidate)
                items.Add(new LegacyFarsiBulkQueueItem(id, LegacyFarsiBulkQueueOutcome.Skipped, null));
            else
                items.Add(ToItem(id, await _requests.Request(id, cultureId, applicationId, cancellationToken, ContentTranslationPrecondition.NoTranslation)));
        }
        return items;
    }

    private static LegacyFarsiBulkQueueItem ToItem(int contentId, ContentTranslationRequestResult result) => result?.State switch
    {
        null => new(contentId, LegacyFarsiBulkQueueOutcome.NotFound, null),
        ContentTranslationState.Queued when result.Created => new(contentId, LegacyFarsiBulkQueueOutcome.Queued, result.JobId),
        ContentTranslationState.Queued or ContentTranslationState.Processing => new(contentId, LegacyFarsiBulkQueueOutcome.AlreadyQueued, result.JobId),
        ContentTranslationState.Ready => new(contentId, LegacyFarsiBulkQueueOutcome.AlreadyReady, null),
        ContentTranslationState.CultureUnavailable => new(contentId, LegacyFarsiBulkQueueOutcome.CultureUnavailable, null),
        _ => new(contentId, LegacyFarsiBulkQueueOutcome.Skipped, null)
    };
}
