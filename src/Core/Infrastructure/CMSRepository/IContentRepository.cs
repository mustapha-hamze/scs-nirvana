namespace Infrastructure.CMSRepository;

public interface IContentRepository : IRepository<Content>
{
    List<Content> List(int applicationId);
    List<Content> List(int applicationId, int pageIndex);
    List<Content> OurBlogBoxList(int applicationId);
    List<ContentSection> GetContentSections(int contentId);
    List<SectionElement> GetSectionElements(int sectionId);
    List<SectionElement> GetSectionElements(List<int> sectionIds);
    Task UpdateSectionPriority(int sectionId, int priority);
    Task CreateContentCategories(string data, string entity, int contentId);
    Task CreateContentTags(string data, string entity, int contentId);
    Task CreateContentCultures(string data, string entity, int contentId);
    ContentMetadata GetContentMetadata(int contentId);
    Task DeleteAllContentImages(int contentId);
    List<ContentImage> GetAllContentImages(int contentId);
    int ContentCount(int applicationId);
    Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId);
    List<Content> GetContentByIdFull(int id);
    List<Content> GetContentByTypeId(int typeId);
    BlogIndexDto GetContentByTypeId(int typeId, int pageIndex = 1);
    BlogIndexDto GetContentByCategoryId(int categoryId, int pageIndex = 1, int pageSize = 40);
    BlogIndexDto GetContentByCategoryIdByDate(int categoryId, DateTime startDate, DateTime endDate, int pageIndex);
    List<Content> GetContentInCategoryAsBox(int categoryId);
}
