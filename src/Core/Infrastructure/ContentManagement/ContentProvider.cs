using Application.ContentManagement;
using Application.ResultModels;
using Domains.Entities.ContentManagement;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.ContentManagement;

public class ContentProvider : IContentProvider
{
    private readonly ApplicationDbContext _dbContext;
    public ContentProvider(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // See IContentProvider.GetContentForTranslate — legacy/internal path for the Farsi
    // translation/activation flow only.
    public async Task<Content> GetContentForTranslate(int contentId)
    {
        return await _dbContext.Contents
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .Include(c => c.Metadata)
            .Include(c => c.Sections.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Elements)
            .FirstOrDefaultAsync(c => c.Id == contentId && !c.IsDeleted);
    }

    public ContentListResultModel GetContentsListByCategoryId(int applicationId, int categoryId, int pageIndex = 0, int pageSize = 20, string keyLang = "en")
    {
        // Was: c.Categories.Contains(categoryId.ToString()), a substring match that also matched
        // e.g. category "1" against a content tagged "11". Now uses the ContentInCategories join
        // table, which matches on the exact category relation instead.
        var categoryContentIds = _dbContext.ContentInCategories.Where(cc => cc.CategoryId == categoryId).Select(cc => cc.ContentId);

        if (keyLang == "fa")
        {
            int rowCount = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && categoryContentIds.Contains(c.Id)).Count();
            int pageCount = rowCount / pageSize;
            if ((rowCount % pageSize) > 0)
                pageCount++;

            int skipSize = (pageIndex * pageSize);

            var contents = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && categoryContentIds.Contains(c.Id))
                .OrderByDescending(c => c.UpdatedDT)
                .Skip(skipSize)
                .Take(pageSize)
                .Include(c => c.Images.Where(i => !i.IsDeleted));

            var category = _dbContext.Categories.Single(c => c.Id == categoryId);

            var result = new ContentListResultModel();
            result.CurrentPage = pageIndex;
            result.PageCount = pageCount;
            result.Contents = contents.ToList();
            result.Title = category.Title;

            return result;
        }
        else
        {
            int rowCount = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && categoryContentIds.Contains(c.Id)).Count();
            int pageCount = rowCount / pageSize;
            if ((rowCount % pageSize) > 0)
                pageCount++;

            int skipSize = (pageIndex * pageSize);

            var contents = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && categoryContentIds.Contains(c.Id))
                .OrderByDescending(c => c.UpdatedDT)
                .Skip(skipSize)
                .Take(pageSize)
                .Include(c => c.Images.Where(i => !i.IsDeleted));

            var category = _dbContext.Categories.Single(c => c.Id == categoryId);

            var result = new ContentListResultModel();
            result.CurrentPage = pageIndex;
            result.PageCount = pageCount;
            result.Contents = contents.ToList();
            result.Title = category.Title;

            return result;
        }
    }

    public ContentListResultModel GetContentsListByTagId(int applicationId, int tagId, int pageIndex = 0, int pageSize = 20)
    {
        // Was: c.Tags.Contains(tagId.ToString()), a substring match that also matched e.g. tag
        // "1" against a content tagged "11". Now uses the ContentInTags join table, which matches
        // on the exact tag relation instead.
        var tagContentIds = _dbContext.ContentInTags.Where(ct => ct.TagId == tagId).Select(ct => ct.ContentId);

        int rowCount = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && tagContentIds.Contains(c.Id)).Count();
        int pageCount = rowCount / pageSize;
        if ((rowCount % pageSize) > 0)
            pageCount++;

        int skipSize = (pageIndex * pageSize);

        var contents = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && tagContentIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedDT)
            .Skip(skipSize)
            .Take(pageSize)
            .Include(c => c.Images.Where(i => !i.IsDeleted));

        var tag = _dbContext.Tags.Single(c => c.Id == tagId);

        var result = new ContentListResultModel();
        result.CurrentPage = pageIndex;
        result.PageCount = pageCount;
        result.Contents = contents.ToList();
        result.Title = tag.Title;

        return result;
    }
}
