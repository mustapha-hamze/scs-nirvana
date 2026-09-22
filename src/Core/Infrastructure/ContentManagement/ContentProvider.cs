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
    // translation/activation flow only. applicationId is required; there is deliberately no
    // bare-contentId overload, so a cross-application id can never resolve.
    public async Task<Content> GetContentForTranslate(int contentId, int applicationId, CancellationToken cancellationToken = default)
    {
        var content = await _dbContext.Contents
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .Include(c => c.Metadata)
            .Include(c => c.Sections.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Elements.Where(e => !e.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == contentId && c.ApplicationId == applicationId && !c.IsDeleted, cancellationToken);

        // Metadata is a single reference, not a collection, so EF's filtered Include can't
        // exclude a soft-deleted row directly — null it out here instead.
        if (content?.Metadata?.IsDeleted == true)
            content.Metadata = null;

        return content;
    }

    public async Task<ContentListResultModel> GetContentsListByCategoryId(int applicationId, int categoryId, int pageIndex = 0, int pageSize = 20, string keyLang = "en", CancellationToken cancellationToken = default)
    {
        // Missing, deleted, or wrong-application category: same empty result as "no content
        // matched" — never distinguishable from a category that just has no content.
        var category = await _dbContext.Categories.SingleOrDefaultAsync(c => c.Id == categoryId && c.ApplicationId == applicationId && c.IsActive && !c.IsDeleted, cancellationToken);
        if (category == null)
        {
            return new ContentListResultModel
            {
                CurrentPage = pageIndex,
                PageCount = 0,
                Contents = new List<Content>(),
                Title = null
            };
        }

        // Was: c.Categories.Contains(categoryId.ToString()), a substring match that also matched
        // e.g. category "1" against a content tagged "11". Now uses the ContentInCategories join
        // table, which matches on the exact category relation instead.
        var categoryContentIds = _dbContext.ContentInCategories.Where(cc => cc.CategoryId == categoryId).Select(cc => cc.ContentId);

        int rowCount = await _dbContext.Contents.CountAsync(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && categoryContentIds.Contains(c.Id), cancellationToken);
        int pageCount = rowCount / pageSize;
        if ((rowCount % pageSize) > 0)
            pageCount++;

        int skipSize = (pageIndex * pageSize);

        var contents = await _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && categoryContentIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedDT)
            .Skip(skipSize)
            .Take(pageSize)
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .ToListAsync(cancellationToken);

        return new ContentListResultModel
        {
            CurrentPage = pageIndex,
            PageCount = pageCount,
            Contents = contents,
            Title = category.Title
        };
    }

    public async Task<ContentListResultModel> GetContentsListByTagId(int applicationId, int tagId, int pageIndex = 0, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        // Missing, deleted, or wrong-application tag: same empty result as "no content matched"
        // — never distinguishable from a tag that just has no content.
        var tag = await _dbContext.Tags.SingleOrDefaultAsync(t => t.Id == tagId && t.ApplicationId == applicationId && t.IsActive && !t.IsDeleted, cancellationToken);
        if (tag == null)
        {
            return new ContentListResultModel
            {
                CurrentPage = pageIndex,
                PageCount = 0,
                Contents = new List<Content>(),
                Title = null
            };
        }

        // Was: c.Tags.Contains(tagId.ToString()), a substring match that also matched e.g. tag
        // "1" against a content tagged "11". Now uses the ContentInTags join table, which matches
        // on the exact tag relation instead.
        var tagContentIds = _dbContext.ContentInTags.Where(ct => ct.TagId == tagId).Select(ct => ct.ContentId);

        int rowCount = await _dbContext.Contents.CountAsync(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && tagContentIds.Contains(c.Id), cancellationToken);
        int pageCount = rowCount / pageSize;
        if ((rowCount % pageSize) > 0)
            pageCount++;

        int skipSize = (pageIndex * pageSize);

        var contents = await _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && tagContentIds.Contains(c.Id))
            .OrderByDescending(c => c.UpdatedDT)
            .Skip(skipSize)
            .Take(pageSize)
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .ToListAsync(cancellationToken);

        return new ContentListResultModel
        {
            CurrentPage = pageIndex,
            PageCount = pageCount,
            Contents = contents,
            Title = tag.Title
        };
    }
}