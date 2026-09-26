using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;

namespace Application.CMSRepository;

// Persistence port for the legacy FarsiContent backfill. Deliberately cross-tenant (an operator
// job over every application's content); never expose it to a request-scoped caller. Reads and
// stages only - ContentTranslationBackfill owns the transaction and save.
public interface IContentTranslationBackfillRepository
{
    Task<bool> CultureExists(int cultureId, CancellationToken cancellationToken = default);

    // Tracked, so checkpoint advancement is staged for the caller's save.
    Task<ContentTranslationBackfillCheckpoint> GetCheckpoint(string runKey, CancellationToken cancellationToken = default);

    void AddCheckpoint(ContentTranslationBackfillCheckpoint checkpoint);

    // Keyset page: up to `take` non-deleted contents with Id > afterContentId and non-blank
    // FarsiContent, ordered by Id, with non-deleted metadata/sections/elements, read untracked.
    Task<List<Content>> GetLegacyFarsiBatch(int afterContentId, int take, CancellationToken cancellationToken = default);

    // ContentIds (from contentIds) that already have a translation row for the culture,
    // including soft-deleted rows - the unique (ContentId, CultureId) key covers those too.
    Task<List<int>> GetTranslatedContentIds(IReadOnlyCollection<int> contentIds, int cultureId, CancellationToken cancellationToken = default);

    void AddTranslation(ContentTranslation translation);
}
