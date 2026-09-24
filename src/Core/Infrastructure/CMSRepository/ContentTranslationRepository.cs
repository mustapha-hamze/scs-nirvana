using Application.CMSRepository;

namespace Infrastructure.CMSRepository;

public class ContentTranslationRepository : IContentTranslationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ContentTranslationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Content> GetSourceGraph(int contentId, CancellationToken cancellationToken = default)
    {
        // AsNoTracking: reads saved database state only, so tracked (possibly soft-deleted)
        // instances in this context aren't fixed up into the navigations. The global IsDeleted
        // filter applies to the included navigations too.
        return _dbContext.Contents.AsNoTracking()
            .Include(c => c.Metadata)
            .Include(c => c.Sections).ThenInclude(s => s.Elements)
            .AsSplitQuery()
            .SingleAsync(c => c.Id == contentId, cancellationToken);
    }

    public Task<List<ContentTranslation>> GetTranslations(int contentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ContentTranslations
            .Where(t => t.ContentId == contentId)
            .ToListAsync(cancellationToken);
    }
}
