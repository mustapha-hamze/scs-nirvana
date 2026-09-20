using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;
using Application.Contracts.CMSApi;
using System;

namespace Services.CMSServices
{
    public interface IContentServices
    {
        Task<ContentDto> Create(ContentDto content);
        Task Delete(int id, int applicationId);
        Task ChangeContentActiveMode(int id, bool mode, int applicationId);
        Task<ContentDto> Update(ContentDto content, int applicationId);
        Task<ContentDto> GetById(int id, int applicationId);
        List<ContentDto> List(int applicationId);
        List<ContentDto> List(int applicationId, int pageIndex);
        List<ContentDto> OurBlogBoxList(int applicationId);
        int ContentCount(int applicationId);

        Task<SectionDto> CreateSection(SectionDto section, int applicationId);
        Task<SectionElementDto> CreateSectionElement(SectionElementDto sectionElement, int applicationId);
        Task UpdateSectionElement(SectionElementDto sectionElement, int applicationId);
        Task<List<SectionDto>> GetSections(int contentId, int applicationId);
        Task UpdateSectionPriority(int sectionId, int priority, int applicationId);

        Task CreateContentCategories(List<int> categoryIds, int contentId, int applicationId);
        Task CreateContentTags(List<int> tagIds, int contentId, int applicationId);
        Task CreateContentCultures(List<int> cultureIds, int contentId, int applicationId);

        Task<ContentMetadataDto> GetContentMetadata(int contentId, int applicationId);
        Task<ContentMetadataDto> CreateContentMetadata(ContentMetadataDto contentMetadata, int applicationId);
        Task<ContentMetadataDto> UpdateContentMetadata(ContentMetadataDto contentMetadata, int applicationId);

        Task CreateContentImage(ContentImageDto contentImage, int applicationId);
        Task DeleteAllContentImages(int contentId, int applicationId);
        Task<List<ContentImageDto>> GetAllContentImages(int contentId, int applicationId);
        Task DeleteSection(int sectionId, int applicationId);

        Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId);

        Task UpdateTranslate(int contentId, string translatedContent, int applicationId);
        Task ActivateTranslatedContent(int contentId, string translatedContent, int applicationId);

        List<ContentApiDto> GetContentByIdFull(int id, int applicationId);
        List<ContentApiDto> GetContentByTypeId(int typeId, int applicationId);
        BlogIndexApiDto GetContentByTypeId(int typeId, int applicationId, int pageIndex = 1);
        BlogIndexApiDto GetContentByCategoryId(int categoryId, int applicationId, int pageIndex = 1, int pageSize = 40);
        BlogIndexApiDto GetContentByCategoryIdByDate(int categoryId, int applicationId, DateTime startDate, DateTime endDate, int pageIndex);

        List<ContentApiDto> GetContentInCategoryAsBox(int categoryId, int applicationId);
    }
}