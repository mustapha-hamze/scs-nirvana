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
    Task CreateContentCategories(int contentId, List<int> categoryIds);
    Task CreateContentTags(int contentId, List<int> tagIds);
    Task CreateContentCultures(int contentId, List<int> cultureIds);
    ContentMetadata GetContentMetadata(int contentId);
    Task DeleteAllContentImages(int contentId);
    List<ContentImage> GetAllContentImages(int contentId);
    int ContentCount(int applicationId);

    // Application-scoped lookup: throws (SingleAsync) rather than returning null when the id
    // doesn't exist or belongs to a different application, so the two cases are indistinguishable
    // to the caller and no cross-application data can leak through a "not found" response.
    Task<Content> GetByIdForApplication(int id, int applicationId);
    Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId);

    // Every public-API read below requires applicationId and filters on it — none of these ever
    // fall back to an unscoped query. A missing, deleted, or wrong-application id must produce
    // the same empty/not-found shape as any other id that doesn't exist.
    List<ContentApiDto> GetContentByIdFull(int id, int applicationId);
    List<ContentApiDto> GetContentByTypeId(int typeId, int applicationId);
    BlogIndexApiDto GetContentByTypeId(int typeId, int applicationId, int pageIndex = 1);
    BlogIndexApiDto GetContentByCategoryId(int categoryId, int applicationId, int pageIndex = 1, int pageSize = 40);
    BlogIndexApiDto GetContentByCategoryIdByDate(int categoryId, int applicationId, DateTime startDate, DateTime endDate, int pageIndex);
    List<ContentApiDto> GetContentInCategoryAsBox(int categoryId, int applicationId);
}
