using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;
using Application.Contracts.CMSApi;
using System;

namespace Application.UseCases.CMSServices
{
    public interface IContentServices
    {
        Task<ContentDto> Create(ContentDto content, CancellationToken cancellationToken = default);
        Task Delete(int id, int applicationId, CancellationToken cancellationToken = default);
        Task ChangeContentActiveMode(int id, bool mode, int applicationId, CancellationToken cancellationToken = default);
        Task<ContentDto> Update(ContentDto content, int applicationId, CancellationToken cancellationToken = default);
        Task<ContentDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<ContentDto>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<List<ContentDto>> List(int applicationId, int pageIndex, CancellationToken cancellationToken = default);
        Task<List<ContentDto>> OurBlogBoxList(int applicationId, CancellationToken cancellationToken = default);
        Task<int> ContentCount(int applicationId, CancellationToken cancellationToken = default);

        Task<SectionDto> CreateSection(SectionDto section, int applicationId, CancellationToken cancellationToken = default);
        Task<SectionElementDto> CreateSectionElement(SectionElementDto sectionElement, int applicationId, CancellationToken cancellationToken = default);
        Task UpdateSectionElement(SectionElementDto sectionElement, int applicationId, CancellationToken cancellationToken = default);
        Task<List<SectionDto>> GetSections(int contentId, int applicationId, CancellationToken cancellationToken = default);
        Task UpdateSectionPriority(int sectionId, int priority, int applicationId, CancellationToken cancellationToken = default);

        Task CreateContentCategories(List<int> categoryIds, int contentId, int applicationId, CancellationToken cancellationToken = default);
        Task CreateContentTags(List<int> tagIds, int contentId, int applicationId, CancellationToken cancellationToken = default);
        Task CreateContentCultures(List<int> cultureIds, int contentId, int applicationId, CancellationToken cancellationToken = default);

        Task<ContentMetadataDto> GetContentMetadata(int contentId, int applicationId, CancellationToken cancellationToken = default);
        Task<ContentMetadataDto> CreateContentMetadata(ContentMetadataDto contentMetadata, int applicationId, CancellationToken cancellationToken = default);
        Task<ContentMetadataDto> UpdateContentMetadata(ContentMetadataDto contentMetadata, int applicationId, CancellationToken cancellationToken = default);

        Task CreateContentImage(ContentImageDto contentImage, int applicationId, CancellationToken cancellationToken = default);
        Task DeleteAllContentImages(int contentId, int applicationId, CancellationToken cancellationToken = default);
        Task<List<ContentImageDto>> GetAllContentImages(int contentId, int applicationId, CancellationToken cancellationToken = default);
        Task DeleteSection(int sectionId, int applicationId, CancellationToken cancellationToken = default);

        Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId, CancellationToken cancellationToken = default);

        Task UpdateTranslate(int contentId, string translatedContent, int applicationId, CancellationToken cancellationToken = default);
        Task ActivateTranslatedContent(int contentId, string translatedContent, int applicationId, CancellationToken cancellationToken = default);

        Task<List<ContentApiDto>> GetContentByIdFull(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<ContentApiDto>> GetContentByTypeId(int typeId, int applicationId, CancellationToken cancellationToken = default);
        Task<BlogIndexApiDto> GetContentByTypeId(int typeId, int applicationId, int pageIndex = 1, CancellationToken cancellationToken = default);
        Task<BlogIndexApiDto> GetContentByCategoryId(int categoryId, int applicationId, int pageIndex = 1, int pageSize = 40, CancellationToken cancellationToken = default);
        Task<BlogIndexApiDto> GetContentByCategoryIdByDate(int categoryId, int applicationId, DateTime startDate, DateTime endDate, int pageIndex, CancellationToken cancellationToken = default);

        Task<List<ContentApiDto>> GetContentInCategoryAsBox(int categoryId, int applicationId, CancellationToken cancellationToken = default);
    }
}