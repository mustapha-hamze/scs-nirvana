using System.Threading.Tasks;
using Application.Repository;
using Domains.Entities.ContentManagement;

namespace Application.CMSRepository;

// Application-scoped content commands: Content root plus its child entities (sections,
// elements, metadata, images). Callers must resolve/verify ownership via
// IContentQueryRepository's *ForApplication methods before calling any of these.
public interface IContentCommandRepository : IRepository<Content>
{
    // Purpose-specific, entity-free update paths for the Farsi translation/activation flow.
    // Unlike IRepository<T>.GetById (AsNoTracking), these query the content without
    // AsNoTracking, so EF's change tracker resolves to an already-tracked instance if the
    // caller obtained one earlier in the same request (e.g. via IContentProvider's
    // GetContentForTranslate) instead of creating a second, conflicting tracked instance.
    Task UpdateFarsiContent(int contentId, string farsiContent);
    Task ActivateTranslatedContent(int contentId, string translatedContent);

    Task DeleteAllContentImages(int contentId);
    Task UpdateSectionPriority(int sectionId, int priority);

    Task<ContentSection> CreateSection(ContentSection section);
    Task DeleteSection(int sectionId);
    Task<SectionElement> CreateSectionElement(SectionElement element);
    Task<SectionElement> UpdateElement(SectionElement element);
    Task<ContentMetadata> CreateContentMetadata(ContentMetadata metadata);
    Task<ContentMetadata> UpdateContentMetadata(ContentMetadata metadata);
    Task<ContentImage> CreateContentImage(ContentImage image);
}
