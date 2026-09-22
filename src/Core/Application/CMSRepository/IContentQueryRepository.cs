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
    Task<List<Content>> List(int applicationId, CancellationToken cancellationToken = default);
    Task<List<Content>> List(int applicationId, int pageIndex, CancellationToken cancellationToken = default);
    Task<List<Content>> OurBlogBoxList(int applicationId, CancellationToken cancellationToken = default);
    Task<int> ContentCount(int applicationId, CancellationToken cancellationToken = default);
    Task<List<ContentSection>> GetContentSections(int contentId, CancellationToken cancellationToken = default);
    Task<List<SectionElement>> GetSectionElements(int sectionId, CancellationToken cancellationToken = default);
    Task<List<SectionElement>> GetSectionElements(List<int> sectionIds, CancellationToken cancellationToken = default);
    Task<ContentMetadata> GetContentMetadata(int contentId, CancellationToken cancellationToken = default);
    Task<List<ContentImage>> GetAllContentImages(int contentId, CancellationToken cancellationToken = default);

    // Application-scoped lookup: throws (SingleAsync) rather than returning null when the id
    // doesn't exist or belongs to a different application, so the two cases are indistinguishable
    // to the caller and no cross-application data can leak through a "not found" response.
    Task<Content> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);

    // Ownership-chain resolution for CMS child mutations: each throws (SingleAsync) unless the
    // resource, and every ancestor up to Content, exists, is not soft-deleted, and the resolved
    // Content belongs to applicationId. Callers must resolve through these rather than trusting a
    // DTO's own ContentId/SectionId.
    Task<ContentSection> GetSectionForApplication(int sectionId, int applicationId, CancellationToken cancellationToken = default);
    Task<SectionElement> GetElementForApplication(int elementId, int applicationId, CancellationToken cancellationToken = default);
    Task<ContentMetadata> GetContentMetadataForApplication(int metadataId, int applicationId, CancellationToken cancellationToken = default);

    // Every public-API read below requires applicationId and filters on it — none of these ever
    // fall back to an unscoped query. A missing, deleted, or wrong-application id must produce
    // the same empty/not-found shape as any other id that doesn't exist.
    Task<List<ContentApiDto>> GetContentByIdFull(int id, int applicationId, CancellationToken cancellationToken = default);
    Task<List<ContentApiDto>> GetContentByTypeId(int typeId, int applicationId, CancellationToken cancellationToken = default);
    Task<BlogIndexApiDto> GetContentByTypeId(int typeId, int applicationId, int pageIndex = 1, CancellationToken cancellationToken = default);
    Task<BlogIndexApiDto> GetContentByCategoryId(int categoryId, int applicationId, int pageIndex = 1, int pageSize = 40, CancellationToken cancellationToken = default);
    Task<BlogIndexApiDto> GetContentByCategoryIdByDate(int categoryId, int applicationId, DateTime startDate, DateTime endDate, int pageIndex, CancellationToken cancellationToken = default);
    Task<List<ContentApiDto>> GetContentInCategoryAsBox(int categoryId, int applicationId, CancellationToken cancellationToken = default);
}
