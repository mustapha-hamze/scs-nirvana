using Application.CMSRepository;
using Application.UseCases.TranslatorServices;

namespace Infrastructure.CMSRepository;

public class LegacyFarsiTranslationCandidateRepository : ILegacyFarsiTranslationCandidateRepository
{
    private readonly ApplicationDbContext _dbContext;

    public LegacyFarsiTranslationCandidateRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsCultureAvailable(int cultureId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Cultures.AnyAsync(c => c.Id == cultureId && c.IsActive, cancellationToken);
    }

    public Task<List<ActiveTranslationJob>> FindActiveJobs(LegacyFarsiCandidateFilter filter, CancellationToken cancellationToken = default)
    {
        var candidates = Candidates(filter);
        return _dbContext.ContentTranslationJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.CultureId == filter.CultureId
                && (j.State == ContentTranslationJobState.Queued || j.State == ContentTranslationJobState.Processing)
                && candidates.Any(c => c.Id == j.ContentId))
            .Select(j => new ActiveTranslationJob(j.ContentId, j.SourceFingerprint))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Content>> FindSources(IReadOnlyCollection<int> contentIds, CancellationToken cancellationToken = default)
    {
        // The same non-deleted graph as ContentTranslationJobRepository.FindSourceGraph (the global
        // query filter drops deleted metadata/sections/elements), projected to the fingerprinted
        // fields so FarsiContent and other large columns are never loaded.
        return _dbContext.Contents.AsNoTracking()
            .Where(c => contentIds.Contains(c.Id))
            .Select(c => new Content
            {
                Id = c.Id, TypeId = c.TypeId, Title = c.Title, HeadLine = c.HeadLine, Abstract = c.Abstract, Description = c.Description,
                Metadata = c.Metadata == null ? null : new ContentMetadata
                {
                    Id = c.Metadata.Id, Title = c.Metadata.Title, Author = c.Metadata.Author, Keywords = c.Metadata.Keywords, Description = c.Metadata.Description
                },
                Sections = c.Sections.Select(s => new ContentSection
                {
                    Id = s.Id, Priority = s.Priority, IsActive = s.IsActive,
                    Elements = s.Elements.Select(e => new SectionElement
                    {
                        Id = e.Id, ElementType = e.ElementType, Size = e.Size, IsActive = e.IsActive, TinyText = e.TinyText, EditorText = e.EditorText
                    }).ToList()
                }).ToList()
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<(int Total, List<LegacyFarsiTranslationCandidate> Items)> FindPage(LegacyFarsiCandidateFilter filter,
        IReadOnlyCollection<int> excludedContentIds, LegacyFarsiCandidateSort sort, bool descending, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Candidates(filter).Where(c => !excludedContentIds.Contains(c.Id));
        var total = await query.CountAsync(cancellationToken);

        IOrderedQueryable<Content> ordered = sort switch
        {
            LegacyFarsiCandidateSort.Title => descending ? query.OrderByDescending(c => c.Title) : query.OrderBy(c => c.Title),
            LegacyFarsiCandidateSort.TypeId => descending ? query.OrderByDescending(c => c.TypeId) : query.OrderBy(c => c.TypeId),
            LegacyFarsiCandidateSort.UpdatedAt => descending ? query.OrderByDescending(c => c.UpdatedDT) : query.OrderBy(c => c.UpdatedDT),
            _ => descending ? query.OrderByDescending(c => c.Id) : query.OrderBy(c => c.Id)
        };
        var items = await ordered.ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new LegacyFarsiTranslationCandidate(c.Id, c.Title, c.TypeId, c.IsActive, c.UpdatedDT))
            .ToListAsync(cancellationToken);
        return (total, items);
    }

    private IQueryable<Content> Candidates(LegacyFarsiCandidateFilter filter)
    {
        // FarsiContent is only tested for non-blank in SQL (tabs/newlines stripped because SQL
        // Server's TRIM removes spaces only); it is never selected.
        // IgnoreQueryFilters: a soft-deleted translation still counts - deliberately deleted
        // translations are never resurrected. It disables the global filters for the whole query
        // (this and any query composing it), so IsDeleted is checked explicitly everywhere else.
        var query = _dbContext.Contents.AsNoTracking()
            .Where(c => !c.IsDeleted && c.ApplicationId == filter.ApplicationId
                && filter.TypeIds.Contains(c.TypeId)
                && c.FarsiContent != null
                && c.FarsiContent.Replace("\t", "").Replace("\r", "").Replace("\n", "").Trim() != ""
                && !_dbContext.ContentTranslations.IgnoreQueryFilters()
                    .Any(t => t.ContentId == c.Id && t.CultureId == filter.CultureId));

        if (filter.ContentId is { } contentId)
            query = query.Where(c => c.Id == contentId);
        if (filter.Title != null)
            query = query.Where(c => c.Title.Contains(filter.Title));
        return query;
    }
}
