using Application.CMSRepository;
using Domains.Entities.General;

namespace Infrastructure.CMSRepository;

public class LocalizedContentReadRepository : ILocalizedContentReadRepository
{
    private readonly ApplicationDbContext _dbContext;

    public LocalizedContentReadRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Culture> FindActiveCulture(int applicationId, string key, CancellationToken cancellationToken = default)
    {
        // ToLower on both sides: case-insensitive on every provider, not only CI collations.
        var normalized = key.ToLowerInvariant();
        var matches = await _dbContext.Cultures.AsNoTracking()
            .Where(c => c.ApplicationId == applicationId && c.IsActive && !c.IsDeleted && c.Key.ToLower() == normalized)
            .Take(2)
            .ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }

    public Task<Content> FindMasterGraph(int contentId, int applicationId, CancellationToken cancellationToken = default)
    {
        // The global IsDeleted filter applies to the included navigations too.
        return _dbContext.Contents.AsNoTracking()
            .Include(c => c.Metadata)
            .Include(c => c.Sections).ThenInclude(s => s.Elements)
            .Include(c => c.Images)
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == contentId && c.ApplicationId == applicationId && !c.IsDeleted, cancellationToken);
    }

    public Task<ContentTranslation> FindTranslation(int contentId, int cultureId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ContentTranslations.AsNoTracking()
            .SingleOrDefaultAsync(t => t.ContentId == contentId && t.CultureId == cultureId && !t.IsDeleted, cancellationToken);
    }
}
