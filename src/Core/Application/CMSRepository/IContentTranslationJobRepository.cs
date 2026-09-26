using System;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;

namespace Application.CMSRepository;

// Persistence port for background translation jobs. Cross-tenant by design (the worker serves
// every application); request-scoped callers must check ContentBelongsToApplication first.
public interface IContentTranslationJobRepository
{
    Task<bool> ContentBelongsToApplication(int contentId, int applicationId, CancellationToken cancellationToken = default);

    // Non-deleted culture, or null.
    Task<Culture> FindCulture(int cultureId, CancellationToken cancellationToken = default);

    // Non-deleted content with non-deleted metadata/sections/elements, untracked; null when
    // missing or deleted.
    Task<Content> FindSourceGraph(int contentId, CancellationToken cancellationToken = default);

    // The (ContentId, CultureId) translation row, tracked, including a soft-deleted one (it still
    // occupies the unique key).
    Task<ContentTranslation> FindTranslation(int contentId, int cultureId, CancellationToken cancellationToken = default);

    void AddTranslation(ContentTranslation translation);

    Task<ContentTranslationJob> FindJob(int contentId, int cultureId, string sourceFingerprint, CancellationToken cancellationToken = default);

    void AddJob(ContentTranslationJob job);

    // Oldest-due job a worker may act on: Queued with NextAttemptAt <= utcNow, or Processing with
    // an expired lease. Tracked.
    Task<ContentTranslationJob> FindNextClaimable(DateTime utcNow, CancellationToken cancellationToken = default);

    // Saves staged changes; returns false (and discards them) when an optimistic-concurrency or
    // unique-key conflict shows another writer got there first.
    Task<bool> TrySaveChanges(CancellationToken cancellationToken = default);
}
