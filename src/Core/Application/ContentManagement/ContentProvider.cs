using Application.ResultModels;
using Domains.Entities.ContentManagement;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StackExchange.Redis;

namespace Application.ContentManagement;

public class ContentProvider : IContentProvider
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    public ContentProvider(ApplicationDbContext dbContext, IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _redis = redis;
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
        return await _dbContext.Contents
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .Include(c => c.Metadata)
            .SingleAsync(c => c.Id == contentId && c.IsActive && !c.IsDeleted);
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

    public ContentListResultModel GetContentsListByCategoryId(int applicationId, int categoryId, int pageIndex = 0, int pageSize = 20)
    {
        int rowCount = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && c.Categories.Contains(categoryId.ToString())).Count();
        int pageCount = rowCount / pageSize;
        if ((rowCount % pageSize) > 0)
            pageCount++;

        int skipSize = (pageIndex * pageSize);

        var contents = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && c.Categories.Contains(categoryId.ToString()))
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

    public ContentListResultModel GetContentsListByTagId(int applicationId, int tagId, int pageIndex = 0, int pageSize = 20)
    {
        int rowCount = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && c.Tags.Contains(tagId.ToString())).Count();
        int pageCount = rowCount / pageSize;
        if ((rowCount % pageSize) > 0)
            pageCount++;

        int skipSize = (pageIndex * pageSize);

        var contents = _dbContext.Contents.Where(c => !c.IsDeleted && c.IsActive && c.ApplicationId == applicationId && c.Tags.Contains(tagId.ToString()))
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

    public void IncreaseViewedTime(int contentId, int applicationId)
    {
        string publicId = applicationId.ToString() + contentId.ToString(); ;
        var db = _redis.GetDatabase();
        var content = db.HashGet("hash_CMS_ContentsViewCount", publicId);

        long viewCount = 0;
        if (!string.IsNullOrEmpty(content))
        {
            viewCount = long.Parse(content) + 1;
            db.HashSet("hash_CMS_ContentsViewCount", new HashEntry[] { new HashEntry(publicId, viewCount) });
        }
        else
        {
            db.HashSet("hash_CMS_ContentsViewCount", new HashEntry[] { new HashEntry(publicId, 1) });
        }
    }

    public async Task<List<Content>> GetMostViewedContent(int applicationId)
    {
        var db = _redis.GetDatabase();

        var records = db.HashGetAll("hash_CMS_ContentsViewCount")
            .Select(entry => new
            {
                Field = entry.Name.ToString(),
                Value = int.Parse(entry.Value.ToString())
            })
            .Where(record => record.Field.StartsWith(applicationId.ToString()))
            .OrderByDescending(record => record.Value)
            .Take(10)
            .ToList();

        int[] contentIds = records.Select(record => int.Parse(record.Field.ToString().Replace(applicationId.ToString(), ""))).ToArray();

        var mostViewedContents = new List<Content>();
        foreach (var contentId in contentIds)
        {
            var content = await _dbContext.Contents.Where(c => c.Id == contentId && c.IsActive && !c.IsDeleted)
            .Include(c => c.Images.Where(i => !i.IsDeleted)).ToListAsync();

            if (content.Count > 0)
                mostViewedContents.Add(content[0]);
        }

        return mostViewedContents;
    }
}
