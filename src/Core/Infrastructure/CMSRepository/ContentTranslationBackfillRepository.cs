using Application.CMSRepository;

namespace Infrastructure.CMSRepository;

public class ContentTranslationBackfillRepository : IContentTranslationBackfillRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ContentTranslationBackfillRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> CultureExists(int cultureId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Cultures.AnyAsync(c => c.Id == cultureId, cancellationToken);
    }

    public Task<ContentTranslationBackfillCheckpoint> GetCheckpoint(string runKey, CancellationToken cancellationToken = default)
    {
        return _dbContext.ContentTranslationBackfillCheckpoints.SingleOrDefaultAsync(c => c.RunKey == runKey, cancellationToken);
    }

    public void AddCheckpoint(ContentTranslationBackfillCheckpoint checkpoint)
    {
        _dbContext.ContentTranslationBackfillCheckpoints.Add(checkpoint);
    }

    public Task<List<Content>> GetLegacyFarsiBatch(int afterContentId, int take, CancellationToken cancellationToken = default)
    {
        // AsNoTracking: the backfill never writes Content, so FarsiContent can't be touched by
        // the batch's save. The global IsDeleted filter applies to the included navigations too.
        return _dbContext.Contents.AsNoTracking()
            .Where(c => c.Id > afterContentId && !string.IsNullOrWhiteSpace(c.FarsiContent))
            .OrderBy(c => c.Id)
            .Take(take)
            .Include(c => c.Metadata)
            .Include(c => c.Sections).ThenInclude(s => s.Elements)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public Task<List<int>> GetTranslatedContentIds(IReadOnlyCollection<int> contentIds, int cultureId, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: a soft-deleted row still occupies the unique (ContentId, CultureId)
        // key, so it must count as existing rather than be re-inserted.
        return _dbContext.ContentTranslations.IgnoreQueryFilters()
            .Where(t => t.CultureId == cultureId && contentIds.Contains(t.ContentId))
            .Select(t => t.ContentId)
            .ToListAsync(cancellationToken);
    }

    public void AddTranslation(ContentTranslation translation)
    {
        _dbContext.ContentTranslations.Add(translation);
    }
}
