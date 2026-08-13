using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;
using Application.Contracts.CMSApi;
using Application.Repository;
using Domains.Entities.ContentManagement;

namespace Application.CMSRepository;

public interface IContentRepository : IRepository<Content>
{
    List<Content> List(int applicationId);
    List<Content> List(int applicationId, int pageIndex);
    List<Content> OurBlogBoxList(int applicationId);
    List<ContentSection> GetContentSections(int contentId);
    List<SectionElement> GetSectionElements(int sectionId);
    List<SectionElement> GetSectionElements(List<int> sectionIds);
    Task UpdateSectionPriority(int sectionId, int priority);

    // Purpose-specific, entity-free update paths for the Farsi translation/activation flow.
    // Unlike IRepository<T>.GetById (AsNoTracking), these query the content without
    // AsNoTracking, so EF's change tracker resolves to an already-tracked instance if the
    // caller obtained one earlier in the same request (e.g. via IContentProvider's
    // GetContentForTranslate) instead of creating a second, conflicting tracked instance.
    Task UpdateFarsiContent(int contentId, string farsiContent);
    Task ActivateTranslatedContent(int contentId, string translatedContent);
    Task CreateContentCategories(string data, string entity, int contentId);
    Task CreateContentTags(string data, string entity, int contentId);
    Task CreateContentCultures(string data, string entity, int contentId);
    ContentMetadata GetContentMetadata(int contentId);
    Task DeleteAllContentImages(int contentId);
    List<ContentImage> GetAllContentImages(int contentId);
    int ContentCount(int applicationId);
    Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId);
    List<ContentApiDto> GetContentByIdFull(int id);
    List<ContentApiDto> GetContentByTypeId(int typeId);
    BlogIndexApiDto GetContentByTypeId(int typeId, int pageIndex = 1);
    BlogIndexApiDto GetContentByCategoryId(int categoryId, int pageIndex = 1, int pageSize = 40);
    BlogIndexApiDto GetContentByCategoryIdByDate(int categoryId, DateTime startDate, DateTime endDate, int pageIndex);
    List<ContentApiDto> GetContentInCategoryAsBox(int categoryId);
}
