using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;

namespace Application.UseCases.TranslatorServices;

public enum LegacyFarsiCandidateSort
{
    Id,
    Title,
    TypeId,
    UpdatedAt
}

// TypeId must be one of ContentTranslation:LegacyBulkCandidateTypeIds (otherwise nothing matches).
public record LegacyFarsiTranslationCandidateQuery(
    int? TypeId = null,
    string Title = null,
    int? ContentId = null,
    LegacyFarsiCandidateSort Sort = LegacyFarsiCandidateSort.Id,
    bool Descending = false,
    int Page = 1,
    int PageSize = LegacyFarsiTranslationCandidates.DefaultPageSize);

public record LegacyFarsiTranslationCandidate(int ContentId, string Title, int TypeId, bool IsActive, DateTime UpdatedAt);

// CultureAvailable is false when ContentTranslation:ActivationCultureId is unset, deleted or inactive.
public record LegacyFarsiTranslationCandidatePage(
    bool CultureAvailable, IReadOnlyList<LegacyFarsiTranslationCandidate> Items, int TotalCount, int Page, int PageSize);

// Read-only list of the application's content with legacy FarsiContent but no translation for the
// configured activation culture and no Queued/Processing job for its current source fingerprint.
// Never writes, never calls the provider, and never reads or returns FarsiContent.
public class LegacyFarsiTranslationCandidates
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    private readonly ILegacyFarsiTranslationCandidateRepository _repository;
    private readonly ContentTranslationOptions _options;

    public LegacyFarsiTranslationCandidates(ILegacyFarsiTranslationCandidateRepository repository, ContentTranslationOptions options)
    {
        _repository = repository;
        _options = options;
    }

    public async Task<LegacyFarsiTranslationCandidatePage> Find(LegacyFarsiTranslationCandidateQuery query, int applicationId,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var cultureId = _options.ActivationCultureId;
        if (cultureId <= 0 || !await _repository.IsCultureAvailable(cultureId, cancellationToken))
            return new LegacyFarsiTranslationCandidatePage(false, [], 0, page, pageSize);

        var allowed = _options.LegacyBulkCandidateTypeIds;
        int[] typeIds = query.TypeId is not { } typeId ? allowed : allowed.Contains(typeId) ? [typeId] : [];
        if (typeIds.Length == 0)
            return new LegacyFarsiTranslationCandidatePage(true, [], 0, page, pageSize);

        var title = string.IsNullOrWhiteSpace(query.Title) ? null : query.Title.Trim();
        var filter = new LegacyFarsiCandidateFilter(applicationId, cultureId, typeIds, title, query.ContentId);

        // A job only excludes content when it is for the current source fingerprint, which is
        // computed here, so only content with an active job for the culture is fingerprinted.
        // ponytail: bounded by the active queue size; precompute fingerprints if that grows large.
        var activeJobs = await _repository.FindActiveJobs(filter, cancellationToken);
        List<int> excluded = [];
        if (activeJobs.Count > 0)
        {
            var sources = await _repository.FindSources(activeJobs.Select(j => j.ContentId).Distinct().ToList(), cancellationToken);
            var fingerprints = sources.ToDictionary(s => s.Id, ContentSourceFingerprint.Compute);
            excluded = activeJobs.Where(j => fingerprints.GetValueOrDefault(j.ContentId) == j.SourceFingerprint)
                .Select(j => j.ContentId).Distinct().ToList();
        }

        var (total, items) = await _repository.FindPage(filter, excluded, query.Sort, query.Descending, page, pageSize, cancellationToken);
        return new LegacyFarsiTranslationCandidatePage(true, items, total, page, pageSize);
    }
}
