using System.Threading.Tasks;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;

namespace Application.CMSRepository;

// Read-only, untracked port for the culture-aware public content read. Everything is scoped to
// the application and excludes soft-deleted rows; nothing here stages or saves changes.
public interface ILocalizedContentReadRepository
{
    // The application's single active, non-deleted culture whose Key equals key ignoring case;
    // null when there is none or the match is ambiguous.
    Task<Culture> FindActiveCulture(int applicationId, string key, CancellationToken cancellationToken = default);

    // The application's non-deleted content with its non-deleted metadata, sections, elements and
    // images (FarsiContent included), or null.
    Task<Content> FindMasterGraph(int contentId, int applicationId, CancellationToken cancellationToken = default);

    // The non-deleted (ContentId, CultureId) translation, or null.
    Task<ContentTranslation> FindTranslation(int contentId, int cultureId, CancellationToken cancellationToken = default);
}
