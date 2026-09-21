using Application.CMSRepository;

namespace Infrastructure.CMSRepository;

public class ContentCommandRepository : Repository<Content>, IContentCommandRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Repository<ContentSection> _sectionRepository;
    private readonly Repository<SectionElement> _elementRepository;
    private readonly Repository<ContentMetadata> _contentMetadataRepository;
    private readonly Repository<ContentImage> _contentImageRepository;

    public ContentCommandRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
        _sectionRepository = new Repository<ContentSection>(dbContext);
        _elementRepository = new Repository<SectionElement>(dbContext);
        _contentMetadataRepository = new Repository<ContentMetadata>(dbContext);
        _contentImageRepository = new Repository<ContentImage>(dbContext);
    }

    public Task DeleteAllContentImages(int contentId)
    {
        _dbContext.ContentImages
            .RemoveRange(_dbContext.ContentImages
            .Where(c => c.ContentId == contentId)
            .AsEnumerable());
        return Task.CompletedTask;
    }

    public async Task UpdateSectionPriority(int sectionId, int priority)
    {
        var section = await _dbContext.ContentSections.SingleAsync(cs => cs.Id == sectionId);
        section.Priority = priority;
    }

    public async Task UpdateFarsiContent(int contentId, string farsiContent)
    {
        // No AsNoTracking: if the content is already tracked in this DbContext (e.g. the
        // Farsi-translation flow fetched it earlier via IContentProvider.GetContentForTranslate),
        // this resolves to that same tracked instance instead of creating a conflicting second one.
        var content = await _dbContext.Contents.SingleAsync(c => c.Id == contentId);
        content.FarsiContent = farsiContent;
        content.UpdatedDT = DateTime.Now;
    }

    public async Task ActivateTranslatedContent(int contentId, string translatedContent)
    {
        var content = await _dbContext.Contents.SingleAsync(c => c.Id == contentId);
        content.FarsiContent = translatedContent;
        content.IsActive = true;
        content.UpdatedDT = DateTime.Now;
    }

    public Task<ContentSection> CreateSection(ContentSection section) => _sectionRepository.Create(section);

    public Task DeleteSection(int sectionId) => _sectionRepository.Delete(sectionId);

    public Task<SectionElement> CreateSectionElement(SectionElement element) => _elementRepository.Create(element);

    public Task<SectionElement> UpdateElement(SectionElement element) => _elementRepository.Update(element);

    public Task<ContentMetadata> CreateContentMetadata(ContentMetadata metadata) => _contentMetadataRepository.Create(metadata);

    public Task<ContentMetadata> UpdateContentMetadata(ContentMetadata metadata) => _contentMetadataRepository.Update(metadata);

    public Task<ContentImage> CreateContentImage(ContentImage image) => _contentImageRepository.Create(image);
}
