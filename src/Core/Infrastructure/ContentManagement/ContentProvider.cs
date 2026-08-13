using Application.ContentManagement;
using Application.ResultModels;
using Domains.Entities.ContentManagement;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StackExchange.Redis;

namespace Infrastructure.ContentManagement;

public class ContentProvider : IContentProvider
{
    private readonly ApplicationDbContext _dbContext;
    public ContentProvider(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Content>> GetLatestContents(int applicationId)
    {
        return await _dbContext.Contents.Where(c => c.ApplicationId == applicationId && c.IsActive && !c.IsDeleted)
        .OrderByDescending(c => c.PublishDt)
        .Take(40)
        .Include(c => c.Images.Where(i => !i.IsDeleted))
        .ToListAsync();
        // var db = _redis.GetDatabase();
        // var contents = db.HashGet("hash_CMS_LatestContent", applicationId);

        // if (!string.IsNullOrEmpty(contents))
        // {
        //     JsonSerializerSettings jsonSerializerSettings = new()
        //     {
        //         NullValueHandling = NullValueHandling.Ignore,
        //         ContractResolver = new CamelCasePropertyNamesContractResolver(),
        //     };
        //     var deserializedContents = JsonConvert.DeserializeObject<List<Content>>(contents, jsonSerializerSettings);
        //     deserializedContents.OrderByDescending(c => c.UpdatedDT).ToList();
        //     return Task.FromResult(deserializedContents);
        // }

        // return Task.FromResult(new List<Content>());
    }

    public async Task<Content> GetContent(int contentId)
    {
        // var db = _redis.GetDatabase();
        // var content = db.HashGet("hash_CMS_Contents", publicId);

        // if (!string.IsNullOrEmpty(content))
        // {
        //     JsonSerializerSettings jsonSerializerSettings = new()
        //     {
        //         NullValueHandling = NullValueHandling.Ignore,
        //         ContractResolver = new CamelCasePropertyNamesContractResolver(),
        //     };
        //     var deserializedContent = JsonConvert.DeserializeObject<Content>(content, jsonSerializerSettings);
        //     return Task.FromResult(deserializedContent);
        // }

        // return Task.FromResult(new Content());
        // return await _dbContext.Contents
        //     .Include(c => c.Images.Where(i => !i.IsDeleted))
        //     .Include(c => c.Metadata)
        //     .Include(c => c.Sections.Where(s => !s.IsDeleted))
        //     .FirstOrDefaultAsync(c => c.Id == contentId && c.IsActive && !c.IsDeleted);

        return await _dbContext.Contents
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .Include(c => c.Metadata)
            .Include(c => c.Sections.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Elements)
            .FirstOrDefaultAsync(c => c.Id == contentId && c.IsActive && !c.IsDeleted);
    }

    public async Task<Content> GetContentForTranslate(int contentId)
    {
        return await _dbContext.Contents
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .Include(c => c.Metadata)
            .Include(c => c.Sections.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Elements)
            .FirstOrDefaultAsync(c => c.Id == contentId && !c.IsDeleted);
    }

    public async Task<Content> GetContentForPreview(int contentId)
    {
        // var db = _redis.GetDatabase();
        // var content = db.HashGet("hash_CMS_Contents", publicId);

        // if (!string.IsNullOrEmpty(content))
        // {
        //     JsonSerializerSettings jsonSerializerSettings = new()
        //     {
        //         NullValueHandling = NullValueHandling.Ignore,
        //         ContractResolver = new CamelCasePropertyNamesContractResolver(),
        //     };
        //     var deserializedContent = JsonConvert.DeserializeObject<Content>(content, jsonSerializerSettings);
        //     return Task.FromResult(deserializedContent);
        // }

        // return Task.FromResult(new Content());
        return await _dbContext.Contents
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .Include(c => c.Metadata)
            .SingleAsync(c => c.Id == contentId && !c.IsDeleted);
    }

    public async Task<List<Content>> FeaturedCategory(int typeId)
    {
        return await _dbContext.Contents.Where(x => x.TypeId == typeId && x.IsActive && !x.IsDeleted)
        .OrderByDescending(x => x.CreatedDT)
        .Include(c => c.Images.Where(i => !i.IsDeleted))
        .ToListAsync();
    }

    public async Task<List<Content>> GetNotification(int typeId)
    {
        return await _dbContext.Contents.Where(x => x.TypeId == typeId && x.IsActive && !x.IsDeleted)
        .OrderByDescending(x => x.CreatedDT)
        .ToListAsync();
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
                .Skip(skipSize)
                .Take(pageSize)
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                .OrderByDescending(c => c.UpdatedDT);

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
                .Skip(skipSize)
                .Take(pageSize)
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                .OrderByDescending(c => c.UpdatedDT);

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
            .Skip(skipSize)
            .Take(pageSize)
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .OrderByDescending(c => c.UpdatedDT);

        var tag = _dbContext.Tags.Single(c => c.Id == tagId);

        var result = new ContentListResultModel();
        result.CurrentPage = pageIndex;
        result.PageCount = pageCount;
        result.Contents = contents.ToList();
        result.Title = tag.Title;

        return result;
    }

    // public async Task<List<Content>> GetMostViewedContent(int applicationId)
    // {
    //     var db = _redis.GetDatabase();

    //     var records = db.HashGetAll("hash_CMS_ContentsViewCount")
    //         .Select(entry => new
    //         {
    //             Field = entry.Name.ToString(),
    //             Value = int.Parse(entry.Value.ToString())
    //         })
    //         .Where(record => record.Field.StartsWith(applicationId.ToString()))
    //         .OrderByDescending(record => record.Value)
    //         .Take(10)
    //         .ToList();

    //     int[] contentIds = records.Select(record => int.Parse(record.Field.ToString().Replace(applicationId.ToString(), ""))).ToArray();

    //     var mostViewedContents = new List<Content>();
    //     foreach (var contentId in contentIds)
    //     {
    //         var content = await _dbContext.Contents.Where(c => c.Id == contentId && c.IsActive && !c.IsDeleted)
    //         .Include(c => c.Images.Where(i => !i.IsDeleted)).ToListAsync();

    //         if (content.Count > 0)
    //             mostViewedContents.Add(content[0]);
    //     }

    //     return mostViewedContents;
    // }

    public async Task<List<Content>> GetPage(int pageId)
    {
        var page = _dbContext.Contents.Where(c => c.TypeId == pageId && c.IsActive && !c.IsDeleted)
            .Include(c => c.Sections.Where(s => !s.IsDeleted));

        return await page.ToListAsync();
    }
    public async Task<List<SectionElement>> GetSectionElements(int sectionId)
    {
        var sectionElements = _dbContext.SectionElements.Where(se => se.SectionId == sectionId && se.IsActive && !se.IsDeleted);

        return await sectionElements.ToListAsync();
    }
}
