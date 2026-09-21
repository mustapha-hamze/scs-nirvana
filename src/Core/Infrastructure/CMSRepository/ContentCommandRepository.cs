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

    public Task<ContentSection> CreateSection(ContentSection section) => _sectionRepository.Create(section);

    public Task DeleteSection(int sectionId, CancellationToken cancellationToken = default) => _sectionRepository.Delete(sectionId, cancellationToken);

    public Task<SectionElement> CreateSectionElement(SectionElement element) => _elementRepository.Create(element);

    public Task<SectionElement> UpdateElement(SectionElement element) => _elementRepository.Update(element);

    public Task<ContentMetadata> CreateContentMetadata(ContentMetadata metadata) => _contentMetadataRepository.Create(metadata);

    public Task<ContentMetadata> UpdateContentMetadata(ContentMetadata metadata) => _contentMetadataRepository.Update(metadata);

    public Task<ContentImage> CreateContentImage(ContentImage image) => _contentImageRepository.Create(image);
}
