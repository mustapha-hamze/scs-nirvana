using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Dto.CMSDtos;
using Domains.Entities.ContentManagement;
using System;

namespace Services.CMSServices
{
    public interface IContentServices
    {
        Task<ContentDto> Create(ContentDto content);
        Task Delete(int id);
        Task ChangeContentActiveMode(int id, bool mode);
        Task<ContentDto> Update(ContentDto content);
        Task<Content> Update(Content content);
        Task<ContentDto> GetById(int id);
        List<ContentDto> List(int applicationId);
        List<ContentDto> List(int applicationId, int pageIndex);
        List<ContentDto> OurBlogBoxList(int applicationId);
        int ContentCount(int applicationId);

        Task<SectionDto> CreateSection(SectionDto section);
        Task<SectionElementDto> CreateSectionElement(SectionElementDto sectionElement);
        Task UpdateSectionElement(SectionElementDto sectionElement);
        List<SectionDto> GetSections(int contentId);
        Task UpdateSectionPriority(int sectionId, int priority);

        Task CreateContentCategories(string data, string entity, int contentId);
        Task CreateContentTags(string data, string entity, int contentId);
        Task CreateContentCultures(string data, string entity, int contentId);

        ContentMetadataDto GetContentMetadata(int contentId);
        Task<ContentMetadataDto> CreateContentMetadata(ContentMetadataDto contentMetadata);
        Task<ContentMetadataDto> UpdateContentMetadata(ContentMetadataDto contentMetadata);

        Task CreateContentImage(ContentImageDto contentImage);
        Task DeleteAllContentImages(int contentId);
        List<ContentImageDto> GetAllContentImages(int contentId);
        Task DeleteSection(int sectionId);

        Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId);

        Task UpdateTranslate(int contentId, string translatedContent);

        List<ContentApiDto> GetContentByIdFull(int id);
        List<ContentApiDto> GetContentByTypeId(int typeId);
        BlogIndexApiDto GetContentByTypeId(int typeId, int pageIndex = 1);
        BlogIndexApiDto GetContentByCategoryId(int categoryId, int pageIndex = 1, int pageSize = 40);
        BlogIndexApiDto GetContentByCategoryIdByDate(int categoryId, DateTime startDate, DateTime endDate, int pageIndex);

        List<ContentApiDto> GetContentInCategoryAsBox(int categoryId);
    }
}