using System;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// Translation state of a Content in one culture, judged against the current source fingerprint.
public enum ContentTranslationState
{
    Missing,
    Queued,
    Processing,
    Ready,
    Stale,
    Failed,
    NeedsReview,

    // The culture doesn't exist or is deleted: nothing can be queued or activated for it.
    CultureUnavailable
}

public record ContentTranslationRequestResult(ContentTranslationState State, int? JobId);

// Request-scoped translation use cases. Every method returns null when the content isn't the
// application's (or is deleted) and CultureUnavailable when the culture is missing or deleted.
// Never calls the provider and never reads or writes legacy FarsiContent.
public class ContentTranslationRequests
{
    private readonly IContentTranslationJobRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ContentTranslationRequests(IContentTranslationJobRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    // Idempotent: a matching Ready translation or an active job for the current fingerprint is
    // returned as is; otherwise exactly one job is queued (a terminal job for the same tuple is
    // re-queued - this is the explicit retry).
    public async Task<ContentTranslationRequestResult> Request(int contentId, int cultureId, int applicationId, CancellationToken cancellationToken = default)
    {
        // Two tries: losing the insert/requeue race to a concurrent request means the winner's job
        // now exists, and the second pass returns it.
        for (var attempt = 0; ; attempt++)
        {
            var current = await Load(contentId, cultureId, applicationId, cancellationToken);
            if (current == null)
                return null;
            var (fingerprint, translation, job) = current.Value;

            if (fingerprint == null)
                return new ContentTranslationRequestResult(ContentTranslationState.CultureUnavailable, null);
            if (ContentTranslationJobProcessor.IsReady(translation, fingerprint))
                return new ContentTranslationRequestResult(ContentTranslationState.Ready, null);
            if (job is { State: ContentTranslationJobState.Queued or ContentTranslationJobState.Processing })
                return new ContentTranslationRequestResult(ToState(job.State), job.Id);

            if (job == null)
            {
                job = new ContentTranslationJob { ContentId = contentId, CultureId = cultureId, SourceFingerprint = fingerprint, IsActive = true };
                _repository.AddJob(job);
            }
            else
            {
                job.Version++;
            }

            job.State = ContentTranslationJobState.Queued;
            job.AttemptCount = 0;
            job.NextAttemptAt = _timeProvider.GetUtcNow().UtcDateTime;
            job.ErrorCode = null;
            job.LeaseOwner = null;
            job.LeaseExpiresAt = null;
            job.CompletedAt = null;

            if (await _repository.TrySaveChanges(cancellationToken))
                return new ContentTranslationRequestResult(ContentTranslationState.Queued, job.Id);
            if (attempt == 1)
                throw new InvalidOperationException("Translation request conflicted twice.");
        }
    }

    public async Task<ContentTranslationState?> GetState(int contentId, int cultureId, int applicationId, CancellationToken cancellationToken = default)
    {
        var current = await Load(contentId, cultureId, applicationId, cancellationToken);
        if (current == null)
            return null;
        var (fingerprint, translation, job) = current.Value;

        if (fingerprint == null)
            return ContentTranslationState.CultureUnavailable;
        if (ContentTranslationJobProcessor.IsReady(translation, fingerprint))
            return ContentTranslationState.Ready;
        if (job is { State: ContentTranslationJobState.Queued or ContentTranslationJobState.Processing or ContentTranslationJobState.Failed })
            return ToState(job.State);
        if (translation == null || translation.IsDeleted)
            return ContentTranslationState.Missing;
        if (translation.TranslationStatus == TranslationStatus.NeedsReview)
            return ContentTranslationState.NeedsReview;
        if (translation.TranslationStatus == TranslationStatus.Failed)
            return ContentTranslationState.Failed;
        return translation.SourceFingerprint != fingerprint || translation.TranslationStatus == TranslationStatus.Stale
            ? ContentTranslationState.Stale
            : ContentTranslationState.Missing;
    }

    // null: content not the application's. A null Fingerprint: culture unavailable.
    private async Task<(string Fingerprint, ContentTranslation Translation, ContentTranslationJob Job)?> Load(
        int contentId, int cultureId, int applicationId, CancellationToken cancellationToken)
    {
        if (!await _repository.ContentBelongsToApplication(contentId, applicationId, cancellationToken))
            return null;
        if (await _repository.FindCulture(cultureId, cancellationToken) == null)
            return (null, null, null);

        var source = await _repository.FindSourceGraph(contentId, cancellationToken);
        if (source == null)
            return null;

        var fingerprint = ContentSourceFingerprint.Compute(source);
        return (fingerprint,
            await _repository.FindTranslation(contentId, cultureId, cancellationToken),
            await _repository.FindJob(contentId, cultureId, fingerprint, cancellationToken));
    }

    private static ContentTranslationState ToState(ContentTranslationJobState state) => state switch
    {
        ContentTranslationJobState.Queued => ContentTranslationState.Queued,
        ContentTranslationJobState.Processing => ContentTranslationState.Processing,
        _ => ContentTranslationState.Failed
    };
}
