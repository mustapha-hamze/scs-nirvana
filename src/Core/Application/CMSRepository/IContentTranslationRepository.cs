using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;

namespace Application.CMSRepository;

// Persistence port for translation staleness. Like IContentCommandRepository, callers must verify
// application ownership of contentId first; this port only reads and stages, never saves.
public interface IContentTranslationRepository
{
    // Non-deleted content with its non-deleted metadata, sections and elements, read untracked
    // from the database - so staged source changes must be saved (in-transaction) beforehand.
    Task<Content> GetSourceGraph(int contentId, CancellationToken cancellationToken = default);

    // Non-deleted translation rows for the content, tracked so status changes are staged for the
    // caller's save.
    Task<List<ContentTranslation>> GetTranslations(int contentId, CancellationToken cancellationToken = default);
}
