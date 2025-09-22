using Application.ResultModels;
using Domains.Entities.ContentManagement;

namespace Application.ContentManagement;

public interface IContentProvider
{
    Task<List<Content>> GetLatestContents(int applicationId);
    Task<Content> GetContent(int contentId);
    Task<Content> GetContentForPreview(int contentId);

    Task<List<Content>> FeaturedCategory(int typeId);
    Task<List<Content>> GetNotification(int typeId);
    ContentListResultModel GetContentsListByCategoryId(int applicationId, int categoryId, int pageIndex = 0, int pageSize = 20);
    ContentListResultModel GetContentsListByTagId(int applicationId, int tagId, int pageIndex = 0, int pageSize = 20);
    void IncreaseViewedTime(int contentId, int applicationId);
    Task<List<Content>> GetMostViewedContent(int applicationId);
}
