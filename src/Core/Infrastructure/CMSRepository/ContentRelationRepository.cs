using Application.CMSRepository;

namespace Infrastructure.CMSRepository;

public class ContentRelationRepository : IContentRelationRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public ContentRelationRepository(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    // categoryIds is expected to already be a validated, de-duplicated set (see
    // ContentServices.CreateContentCategories). This only stages the join-row replace and the
    // legacy pipe-delimited compatibility field update; the caller (Application layer) is
    // responsible for running it inside one transaction/SaveChanges so both change together.
    public async Task CreateContentCategories(int contentId, List<int> categoryIds, CancellationToken cancellationToken = default)
    {
        var content = await _dbContext.Contents.SingleAsync(c => c.Id == contentId, cancellationToken);

        content.Categories = string.Join("|", categoryIds);
        _dbContext.Entry(content).State = EntityState.Modified;

        var existing = await _dbContext.ContentInCategories
            .Where(c => c.ContentId == contentId)
            .ToListAsync(cancellationToken);
        _dbContext.ContentInCategories.RemoveRange(existing);

        foreach (var categoryId in categoryIds)
        {
            _dbContext.ContentInCategories.Add(new ContentInCategory
            {
                ContentId = contentId,
                CategoryId = categoryId,
                CreatedDt = _timeProvider.GetUtcNow().UtcDateTime
            });
        }
    }

    public async Task CreateContentTags(int contentId, List<int> tagIds, CancellationToken cancellationToken = default)
    {
        var content = await _dbContext.Contents.SingleAsync(c => c.Id == contentId, cancellationToken);
        content.Tags = string.Join("|", tagIds);
        _dbContext.Entry(content).State = EntityState.Modified;

        var existing = await _dbContext.ContentInTags
            .Where(c => c.ContentId == contentId)
            .ToListAsync(cancellationToken);
        _dbContext.ContentInTags.RemoveRange(existing);

        foreach (var tagId in tagIds)
        {
            _dbContext.ContentInTags.Add(new ContentInTag
            {
                ContentId = contentId,
                TagId = tagId
            });
        }
    }

    public async Task CreateContentCultures(int contentId, List<int> cultureIds, CancellationToken cancellationToken = default)
    {
        var content = await _dbContext.Contents.SingleAsync(c => c.Id == contentId, cancellationToken);
        content.Cultures = string.Join("|", cultureIds);
        _dbContext.Entry(content).State = EntityState.Modified;

        var existing = await _dbContext.ContentInCultures
            .Where(c => c.ContentId == contentId)
            .ToListAsync(cancellationToken);
        _dbContext.ContentInCultures.RemoveRange(existing);

        foreach (var cultureId in cultureIds)
        {
            _dbContext.ContentInCultures.Add(new ContentInCulture
            {
                ContentId = contentId,
                CultureId = cultureId
            });
        }
    }
}
