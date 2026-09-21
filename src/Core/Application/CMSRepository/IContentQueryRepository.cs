using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMSApi;
using Domains.Entities.ContentManagement;

namespace Application.CMSRepository;

// EF read projections only: no write operations, and every returned entity is AsNoTracking (or
// a DTO/projection), so nothing here can be mutated and saved back through this port.
public interface IContentQueryRepository
{
    List<Content> List(int applicationId);
    List<Content> List(int applicationId, int pageIndex);
    List<Content> OurBlogBoxList(int applicationId);
    int ContentCount(int applicationId);
    List<ContentSection> GetContentSections(int contentId);
    List<SectionElement> GetSectionElements(int sectionId);
    List<SectionElement> GetSectionElements(List<int> sectionIds);
    ContentMetadata GetContentMetadata(int contentId);
    List<ContentImage> GetAllContentImages(int contentId);

    // Application-scoped lookup: throws (SingleAsync) rather than returning null when the id
    // doesn't exist or belongs to a different application, so the two cases are indistinguishable
    // to the caller and no cross-application data can leak through a "not found" response.
    Task<Content> GetByIdForApplication(int id, int applicationId);

    // Ownership-chain resolution for CMS child mutations: each throws (SingleAsync) unless the
    // resource, and every ancestor up to Content, exists, is not soft-deleted, and the resolved
    // Content belongs to applicationId. Callers must resolve through these rather than trusting a
    // DTO's own ContentId/SectionId.
    Task<ContentSection> GetSectionForApplication(int sectionId, int applicationId);
    Task<SectionElement> GetElementForApplication(int elementId, int applicationId);
    Task<ContentMetadata> GetContentMetadataForApplication(int metadataId, int applicationId);

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
