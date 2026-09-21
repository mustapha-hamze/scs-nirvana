using Application.CMSRepository;

namespace Infrastructure.CMSRepository;

public class ContentRelationRepository : IContentRelationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ContentRelationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // categoryIds is expected to already be a validated, de-duplicated set (see
    // ContentServices.CreateContentCategories). This only stages the join-row replace and the
    // legacy pipe-delimited compatibility field update; the caller (Application layer) is
    // responsible for running it inside one transaction/SaveChanges so both change together.
    public Task CreateContentCategories(int contentId, List<int> categoryIds)
    {
        var content = _dbContext.Contents.Single(c => c.Id == contentId);

        content.Categories = string.Join("|", categoryIds);
        _dbContext.Entry(content).State = EntityState.Modified;

        _dbContext.ContentInCategories
            .RemoveRange(_dbContext.ContentInCategories
            .Where(c => c.ContentId == contentId)
            .AsEnumerable());

        foreach (var categoryId in categoryIds)
        {
            _dbContext.ContentInCategories.Add(new ContentInCategory
            {
                ContentId = contentId,
                CategoryId = categoryId,
                CreatedDt = DateTime.Now
            });
        }

        return Task.CompletedTask;
    }

    public Task CreateContentTags(int contentId, List<int> tagIds)
    {
        var content = _dbContext.Contents.Single(c => c.Id == contentId);
        content.Tags = string.Join("|", tagIds);
        _dbContext.Entry(content).State = EntityState.Modified;

        _dbContext.ContentInTags
            .RemoveRange(_dbContext.ContentInTags
            .Where(c => c.ContentId == contentId)
            .AsEnumerable());

        foreach (var tagId in tagIds)
        {
            _dbContext.ContentInTags.Add(new ContentInTag
            {
                ContentId = contentId,
                TagId = tagId
            });
        }

        return Task.CompletedTask;
    }

    public Task CreateContentCultures(int contentId, List<int> cultureIds)
    {
        var content = _dbContext.Contents.Single(c => c.Id == contentId);
        content.Cultures = string.Join("|", cultureIds);
        _dbContext.Entry(content).State = EntityState.Modified;

        _dbContext.ContentInCultures
            .RemoveRange(_dbContext.ContentInCultures
            .Where(c => c.ContentId == contentId)
            .AsEnumerable());

        foreach (var cultureId in cultureIds)
        {
            _dbContext.ContentInCultures.Add(new ContentInCulture
            {
                ContentId = contentId,
                CultureId = cultureId
            });
        }

        return Task.CompletedTask;
    }
}
