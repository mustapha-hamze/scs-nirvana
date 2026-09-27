using System.Collections.Generic;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;

namespace Application.CMSRepository;

// Candidate filter the repository applies server-side: the application's non-deleted content of
// one of TypeIds with non-blank legacy FarsiContent and no ContentTranslation row (deleted or not)
// for CultureId. Title and ContentId are optional.
public record LegacyFarsiCandidateFilter(int ApplicationId, int CultureId, IReadOnlyCollection<int> TypeIds, string Title, int? ContentId);

public record ActiveTranslationJob(int ContentId, string SourceFingerprint);

// Read-only persistence port for the legacy-Farsi translation candidate list. Every method is
// untracked and never loads FarsiContent.
public interface ILegacyFarsiTranslationCandidateRepository
{
    // Non-deleted, active culture.
    Task<bool> IsCultureAvailable(int cultureId, CancellationToken cancellationToken = default);

    // Queued or Processing jobs for the filter's culture on content matching the filter.
    Task<List<ActiveTranslationJob>> FindActiveJobs(LegacyFarsiCandidateFilter filter, CancellationToken cancellationToken = default);

    // The fingerprinted source fields only (ContentSourceFingerprint input), without FarsiContent.
    Task<List<Content>> FindSources(IReadOnlyCollection<int> contentIds, CancellationToken cancellationToken = default);

    // Total and one page of the filter's content minus excludedContentIds, ordered by sort then Id.
    Task<(int Total, List<LegacyFarsiTranslationCandidate> Items)> FindPage(LegacyFarsiCandidateFilter filter,
        IReadOnlyCollection<int> excludedContentIds, LegacyFarsiCandidateSort sort, bool descending, int page, int pageSize,
        CancellationToken cancellationToken = default);
}
