using Application.CMSRepository;

namespace Infrastructure.CMSRepository;

public class ContentCommandRepository : Repository<Content>, IContentCommandRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ContentCommandRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Delete(int id, int applicationId, CancellationToken cancellationToken = default)
    {
        var content = await _dbContext.Contents
            .SingleAsync(c => c.Id == id && c.ApplicationId == applicationId && !c.IsDeleted, cancellationToken);
        _dbContext.Contents.Remove(content);
    }

    public async Task DeleteAllContentImages(int contentId, CancellationToken cancellationToken = default)
    {
        var images = await _dbContext.ContentImages
            .Where(c => c.ContentId == contentId)
            .ToListAsync(cancellationToken);
        _dbContext.ContentImages.RemoveRange(images);
    }

    public async Task UpdateSectionPriority(int sectionId, int priority, CancellationToken cancellationToken = default)
    {
        var section = await _dbContext.ContentSections.SingleAsync(cs => cs.Id == sectionId, cancellationToken);
        section.Priority = priority;
    }

    public async Task UpdateFarsiContent(int contentId, string farsiContent, CancellationToken cancellationToken = default)
    {
        // No AsNoTracking: if the content is already tracked in this DbContext (e.g. the
        // Farsi-translation flow fetched it earlier via IContentProvider.GetContentForTranslate),
        // this resolves to that same tracked instance instead of creating a conflicting second one.
        var content = await _dbContext.Contents.SingleAsync(c => c.Id == contentId, cancellationToken);
        content.FarsiContent = farsiContent;
    }

    public async Task ActivateTranslatedContent(int contentId, string translatedContent, CancellationToken cancellationToken = default)
    {
        var content = await _dbContext.Contents.SingleAsync(c => c.Id == contentId, cancellationToken);
        content.FarsiContent = translatedContent;
        content.IsActive = true;
    }

    public async Task ActivateExistingContent(int contentId, CancellationToken cancellationToken = default)
    {
        // No AsNoTracking, same reasoning as UpdateFarsiContent above: resolves to the
        // already-tracked instance from IContentProvider.GetContentForTranslate when called from
        // the existing-Farsi activation branch, instead of creating a conflicting second one.
        var content = await _dbContext.Contents.SingleAsync(c => c.Id == contentId, cancellationToken);
        content.IsActive = true;
    }

    public Task<ContentSection> CreateSection(ContentSection section)
    {
        _dbContext.ContentSections.Add(section);
        _dbContext.Entry(section).State = EntityState.Added;
        return Task.FromResult(section);
    }

    public async Task DeleteSection(int sectionId, CancellationToken cancellationToken = default)
    {
        var section = await _dbContext.ContentSections.SingleAsync(s => s.Id == sectionId, cancellationToken);
        _dbContext.ContentSections.Remove(section);
    }

    public Task<SectionElement> CreateSectionElement(SectionElement element)
    {
        _dbContext.SectionElements.Add(element);
        _dbContext.Entry(element).State = EntityState.Added;
        return Task.FromResult(element);
    }

    public Task<SectionElement> UpdateElement(SectionElement element)
    {
        _dbContext.SectionElements.Update(element);
        _dbContext.Entry(element).State = EntityState.Modified;
        return Task.FromResult(element);
    }

    public Task<ContentMetadata> CreateContentMetadata(ContentMetadata metadata)
    {
        _dbContext.ContentMetadatas.Add(metadata);
        _dbContext.Entry(metadata).State = EntityState.Added;
        return Task.FromResult(metadata);
    }

    public Task<ContentMetadata> UpdateContentMetadata(ContentMetadata metadata)
    {
        _dbContext.ContentMetadatas.Update(metadata);
        _dbContext.Entry(metadata).State = EntityState.Modified;
        return Task.FromResult(metadata);
    }

    public Task<ContentImage> CreateContentImage(ContentImage image)
    {
        _dbContext.ContentImages.Add(image);
        _dbContext.Entry(image).State = EntityState.Added;
        return Task.FromResult(image);
    }
}
