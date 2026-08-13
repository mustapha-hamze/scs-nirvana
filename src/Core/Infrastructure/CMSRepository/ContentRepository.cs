using Application.UnitOfWork;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.CMSRepository;

public class ContentRepository : Repository<Content>, IContentRepository
{
    // fields
    private readonly ApplicationDbContext _dbContext;
    private readonly string _connectionString;
    private readonly IUnitOfWork _unitOfWork;

    // constructor
    public ContentRepository(ApplicationDbContext dbContext, IConfiguration configuration, IUnitOfWork unitOfWork) : base(dbContext)
    {
        _dbContext = dbContext;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _unitOfWork = unitOfWork;
    }

    // methods
    public List<ContentApiDto> GetContentByIdFull(int id)
    {
        return _dbContext.Contents.Where(c => c.Id == id)
            .Select(c => new ContentApiDto
            {
                Id = c.Id,
                Status = c.Status,
                IsDeleted = c.IsDeleted,
                IsActive = c.IsActive,
                UpdatedDT = c.UpdatedDT,
                CreatedDT = c.CreatedDT,
                ApplicationId = c.ApplicationId,
                TypeId = c.TypeId,
                Title = c.Title,
                HeadLine = c.HeadLine,
                Abstract = c.Abstract,
                Description = c.Description,
                FarsiContent = c.FarsiContent,
                Categories = c.Categories,
                Tags = c.Tags,
                Cultures = c.Cultures,
                PublishDt = c.PublishDt,
                Images = c.Images.Where(i => !i.IsDeleted).Select(i => new ContentImageApiDto
                {
                    Id = i.Id,
                    Status = i.Status,
                    IsDeleted = i.IsDeleted,
                    IsActive = i.IsActive,
                    UpdatedDT = i.UpdatedDT,
                    CreatedDT = i.CreatedDT,
                    ContentId = i.ContentId,
                    ImageFileName = i.ImageFileName,
                    Size = i.Size
                }).ToList(),
                Sections = c.Sections.Where(s => !s.IsDeleted).OrderBy(s => s.Priority).Select(s => new ContentSectionApiDto
                {
                    Id = s.Id,
                    Status = s.Status,
                    IsDeleted = s.IsDeleted,
                    IsActive = s.IsActive,
                    UpdatedDT = s.UpdatedDT,
                    CreatedDT = s.CreatedDT,
                    ContentId = s.ContentId,
                    Priority = s.Priority,
                    Elements = s.Elements.Select(e => new SectionElementApiDto
                    {
                        Id = e.Id,
                        Status = e.Status,
                        IsDeleted = e.IsDeleted,
                        IsActive = e.IsActive,
                        UpdatedDT = e.UpdatedDT,
                        CreatedDT = e.CreatedDT,
                        SectionId = e.SectionId,
                        ElementType = e.ElementType,
                        TinyText = e.TinyText,
                        EditorText = e.EditorText,
                        FileNameText = e.FileNameText,
                        GalleryImages = e.GalleryImages,
                        Size = e.Size,
                        ElementTitle = e.ElementTitle
                    }).ToList()
                }).ToList()
            })
            .ToList();
    }
    public List<ContentApiDto> GetContentByTypeId(int typeId)
    {
        return _dbContext.Contents.Where(c => c.TypeId == typeId && !c.IsDeleted && c.IsActive)
            .Select(c => new ContentApiDto
            {
                Id = c.Id,
                Status = c.Status,
                IsDeleted = c.IsDeleted,
                IsActive = c.IsActive,
                UpdatedDT = c.UpdatedDT,
                CreatedDT = c.CreatedDT,
                ApplicationId = c.ApplicationId,
                TypeId = c.TypeId,
                Title = c.Title,
                HeadLine = c.HeadLine,
                Abstract = c.Abstract,
                Description = c.Description,
                FarsiContent = c.FarsiContent,
                Categories = c.Categories,
                Tags = c.Tags,
                Cultures = c.Cultures,
                PublishDt = c.PublishDt,
                Images = c.Images.Select(i => new ContentImageApiDto
                {
                    Id = i.Id,
                    Status = i.Status,
                    IsDeleted = i.IsDeleted,
                    IsActive = i.IsActive,
                    UpdatedDT = i.UpdatedDT,
                    CreatedDT = i.CreatedDT,
                    ContentId = i.ContentId,
                    ImageFileName = i.ImageFileName,
                    Size = i.Size
                }).ToList(),
                // Matches the previous .Include(c => c.Sections) with no ThenInclude(Elements):
                // sections are populated, their elements are not.
                Sections = c.Sections.Select(s => new ContentSectionApiDto
                {
                    Id = s.Id,
                    Status = s.Status,
                    IsDeleted = s.IsDeleted,
                    IsActive = s.IsActive,
                    UpdatedDT = s.UpdatedDT,
                    CreatedDT = s.CreatedDT,
                    ContentId = s.ContentId,
                    Priority = s.Priority
                }).ToList(),
                Metadata = c.Metadata == null ? null : new ContentMetadataApiDto
                {
                    Id = c.Metadata.Id,
                    Status = c.Metadata.Status,
                    IsDeleted = c.Metadata.IsDeleted,
                    IsActive = c.Metadata.IsActive,
                    UpdatedDT = c.Metadata.UpdatedDT,
                    CreatedDT = c.Metadata.CreatedDT,
                    ContentId = c.Metadata.ContentId,
                    Title = c.Metadata.Title,
                    Author = c.Metadata.Author,
                    Keywords = c.Metadata.Keywords,
                    Description = c.Metadata.Description
                }
            })
            .ToList();
    }
    public BlogIndexApiDto GetContentByTypeId(int typeId, int pageIndex = 1)
    {
        var result = new BlogIndexApiDto();
        int skipCount = 0;
        if (pageIndex > 1)
            skipCount = 15 * (pageIndex - 1);

        result.Contents = _dbContext.Contents
            .Where(c => c.TypeId == typeId && !c.IsDeleted && c.IsActive)
            .Select(c => new ContentApiDto
            {
                Id = c.Id,
                Title = c.Title,
                Abstract = c.Abstract,
                HeadLine = c.HeadLine,
                CreatedDT = c.CreatedDT,
                Categories = c.Categories,
                Images = c.Images.Where(ci => ci.Size == 640).Select(i => new ContentImageApiDto
                {
                    Id = i.Id,
                    Status = i.Status,
                    IsDeleted = i.IsDeleted,
                    IsActive = i.IsActive,
                    UpdatedDT = i.UpdatedDT,
                    CreatedDT = i.CreatedDT,
                    ContentId = i.ContentId,
                    ImageFileName = i.ImageFileName,
                    Size = i.Size
                }).ToList()
            })
            .OrderByDescending(c => c.CreatedDT)
            .Skip(skipCount).Take(15).ToList();

        var rowsCount = _dbContext.Contents.Count(c => c.TypeId == typeId && !c.IsDeleted && c.IsActive);
        var pageCount = rowsCount / 15;
        if ((rowsCount % 15) > 0)
            pageCount++;

        result.PagesCount = pageCount;
        result.PageIndex = pageIndex;

        return result;
    }

    public BlogIndexApiDto GetContentByCategoryId(int categoryId, int pageIndex = 1, int pageSize = 40)
    {
        // NOTE: PagesCount/PageIndex are intentionally left at their default (0) below, matching
        // the pre-existing behavior of this endpoint — it has never computed pagination metadata.
        var result = new BlogIndexApiDto();

        var query = from contentCategory in _dbContext.ContentInCategories
                    join content in _dbContext.Contents on contentCategory.ContentId equals content.Id
                    where contentCategory.CategoryId == categoryId
                          && content.IsActive == true
                          && content.IsDeleted == false
                    orderby contentCategory.CreatedDt descending
                    select new
                    {
                        contentCategory.ContentId,
                        contentCategory.CategoryId,
                        contentCategory.CreatedDt,
                        content.Title,
                        content.Description,
                        ContentCreatedDT = content.CreatedDT,
                        content.Abstract,
                        content.HeadLine,
                        content.Tags,
                        content.Cultures,
                        content.Status,
                        content.TypeId,
                    };

        var _result = query
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        var contentIds = _result.Select(r => r.ContentId).ToList();
        var imagesByContentId = _dbContext.ContentImages
            .Where(i => contentIds.Contains(i.ContentId) && (i.Size == 640 || i.Size == 430 || i.Size == 860))
            .ToList()
            .GroupBy(i => i.ContentId)
            .ToDictionary(g => g.Key, g => g.Select(i => new ContentImageApiDto
            {
                Id = i.Id,
                Status = i.Status,
                IsDeleted = i.IsDeleted,
                IsActive = i.IsActive,
                UpdatedDT = i.UpdatedDT,
                CreatedDT = i.CreatedDT,
                ContentId = i.ContentId,
                ImageFileName = i.ImageFileName,
                Size = i.Size
            }).ToList());

        List<ContentApiDto> contents = new();
        foreach (var item in _result)
        {
            contents.Add(new ContentApiDto
            {
                Id = item.ContentId,
                Title = item.Title,
                Abstract = item.Abstract,
                HeadLine = item.HeadLine,
                Description = item.Description,
                CreatedDT = item.ContentCreatedDT,
                Images = imagesByContentId.TryGetValue(item.ContentId, out var images) ? images : new List<ContentImageApiDto>()
            });
        }

        result.Contents = contents.OrderByDescending(c => c.CreatedDT).ToList();

        return result;
    }

    public List<ContentApiDto> GetContentInCategoryAsBox(int categoryId)
    {
        var query = (from ccc in _dbContext.ContentInCategories
                     join cc in _dbContext.Contents on ccc.ContentId equals cc.Id
                     where cc.IsDeleted == false && cc.IsActive == true && ccc.CategoryId == categoryId
                     orderby ccc.CreatedDt descending
                     select new
                     {
                         ContentId = ccc.ContentId,
                         CategoryId = ccc.CategoryId,
                         CreatedDt = ccc.CreatedDt,
                         Title = cc.Title,
                         Description = cc.Description,
                         HeadLine = cc.HeadLine,
                         Abstract = cc.Abstract
                     }).Take(10);

        var result = query.ToList();

        var contentIds = result.Select(r => r.ContentId).ToList();
        var imagesByContentId = _dbContext.ContentImages
            .Where(i => contentIds.Contains(i.ContentId) && i.Size == 640)
            .ToList()
            .GroupBy(i => i.ContentId)
            .ToDictionary(g => g.Key, g => g.Select(i => new ContentImageApiDto
            {
                Id = i.Id,
                Status = i.Status,
                IsDeleted = i.IsDeleted,
                IsActive = i.IsActive,
                UpdatedDT = i.UpdatedDT,
                CreatedDT = i.CreatedDT,
                ContentId = i.ContentId,
                ImageFileName = i.ImageFileName,
                Size = i.Size
            }).ToList());

        List<ContentApiDto> contents = new();
        foreach (var item in result)
        {
            contents.Add(new ContentApiDto
            {
                Id = item.ContentId,
                Title = item.Title,
                Abstract = item.Abstract,
                HeadLine = item.HeadLine,
                Description = item.Description,
                Images = imagesByContentId.TryGetValue(item.ContentId, out var images) ? images : new List<ContentImageApiDto>()
            });
        }

        return contents;
    }

    public BlogIndexApiDto GetContentByCategoryIdByDate(int categoryId, DateTime startDate, DateTime endDate, int pageIndex = 1)
    {
        var result = new BlogIndexApiDto();
        int skipCount = 0;
        if (pageIndex > 1)
            skipCount = 15 * (pageIndex - 1);

        // Was: c.Categories.Contains(categoryId.ToString()), a substring match that also matched e.g.
        // category "1" against a content tagged "11". Now uses the ContentInCategories join table,
        // which matches on the exact category relation instead.
        var categoryFilter = _dbContext.ContentInCategories.Where(cc => cc.CategoryId == categoryId).Select(cc => cc.ContentId);

        result.Contents = _dbContext.Contents
            .Where(c => categoryFilter.Contains(c.Id) && !c.IsDeleted && c.IsActive
                && c.CreatedDT > startDate && c.CreatedDT < endDate)
            .Select(c => new ContentApiDto
            {
                Id = c.Id,
                Title = c.Title,
                Abstract = c.Abstract,
                HeadLine = c.HeadLine,
                CreatedDT = c.CreatedDT,
                Categories = c.Categories,
                Images = c.Images.Where(ci => ci.Size == 640).Select(i => new ContentImageApiDto
                {
                    Id = i.Id,
                    Status = i.Status,
                    IsDeleted = i.IsDeleted,
                    IsActive = i.IsActive,
                    UpdatedDT = i.UpdatedDT,
                    CreatedDT = i.CreatedDT,
                    ContentId = i.ContentId,
                    ImageFileName = i.ImageFileName,
                    Size = i.Size
                }).ToList()
            })
            .OrderByDescending(c => c.CreatedDT)
            .Skip(skipCount).Take(15).ToList();

        var rowsCount = _dbContext.Contents
            .Count(c => categoryFilter.Contains(c.Id) && !c.IsDeleted && c.IsActive
                && c.CreatedDT > startDate && c.CreatedDT < endDate);
        var pageCount = rowsCount / 15;
        if ((rowsCount % 15) > 0)
            pageCount++;

        result.PagesCount = pageCount;
        result.PageIndex = pageIndex;

        return result;
    }

    public List<Content> List(int applicationId)
    {
        return _dbContext.Contents
            .Where(c => !c.IsDeleted && c.ApplicationId == applicationId)
            .OrderByDescending(c => c.CreatedDT).ToList();
    }

    public List<Content> OurBlogBoxList(int applicationId)
    {
        var contents = _dbContext.Contents
            .Where(c => !c.IsDeleted && c.ApplicationId == applicationId)
            .OrderByDescending(c => c.CreatedDT).Take(3).ToList();

        return contents;
    }

    public List<Content> List(int applicationId, int pageIndex)
    {
        var contents = _dbContext.Contents.Skip(pageIndex * 20).Take(20)
            .Where(c => !c.IsDeleted && c.ApplicationId == applicationId)
            .OrderByDescending(c => c.CreatedDT).ToList();
        return contents;
    }

    public int ContentCount(int applicationId)
    {
        return _dbContext.Contents
            .Count(c => !c.IsDeleted && c.ApplicationId == applicationId);
    }

    public List<ContentSection> GetContentSections(int contentId)
    {
        return _dbContext.ContentSections
            .Where(s => !s.IsDeleted && s.ContentId == contentId)
            .OrderBy(s => s.Priority).ToList();
    }

    public List<SectionElement> GetSectionElements(List<int> sectionIds)
    {
        return _dbContext.SectionElements
            .Where(e => !e.IsDeleted && e.IsActive && sectionIds.Contains(e.SectionId))
            .ToList();
    }

    public List<SectionElement> GetSectionElements(int sectionId)
    {
        return _dbContext.SectionElements
            .Where(e => !e.IsDeleted && e.IsActive && e.SectionId == sectionId)
            .ToList();
    }

    public ContentMetadata GetContentMetadata(int contentId)
    {
        if (_dbContext.ContentMetadatas.Any(m => m.ContentId == contentId))
            return _dbContext.ContentMetadatas.Single(m => m.ContentId == contentId);
        else
            return new ContentMetadata();
    }

    public async Task CreateContentCategories(string data, string entity, int contentId)
    {
        await _unitOfWork.ExecuteInTransactionAsync(() =>
        {
            var content = _dbContext.Contents.Single(c => c.Id == contentId);

            content.Categories = data;
            content.UpdatedDT = DateTime.Now;
            _dbContext.Entry(content).State = EntityState.Modified;

            _dbContext.ContentInCategories
                .RemoveRange(_dbContext.ContentInCategories
                .Where(c => c.ContentId == contentId)
                .AsEnumerable());

            if (data != "" && data != null && data != string.Empty)
            {
                var categories = data.Split('|');
                foreach (var item in categories)
                {
                    if (item != null && item != "" && item != string.Empty)
                    {
                        _dbContext.ContentInCategories.Add(new ContentInCategory
                        {
                            ContentId = contentId,
                            CategoryId = Convert.ToInt32(item),
                            CreatedDt = DateTime.Now
                        });
                    }
                }
            }

            return Task.CompletedTask;
        });
    }

    public async Task CreateContentTags(string data, string entity, int contentId)
    {
        await _unitOfWork.ExecuteInTransactionAsync(() =>
        {
            var content = _dbContext.Contents.Single(c => c.Id == contentId);
            content.Tags = data;
            content.UpdatedDT = DateTime.Now;
            _dbContext.Entry(content).State = EntityState.Modified;

            _dbContext.ContentInTags
                .RemoveRange(_dbContext.ContentInTags
                .Where(c => c.ContentId == contentId)
                .AsEnumerable());

            if (data != "" && data != null && data != string.Empty)
            {
                var tags = data.Split('|');

                foreach (var item in tags)
                {
                    if (item != null && item != "" && item != string.Empty)
                    {
                        _dbContext.ContentInTags.Add(new ContentInTag
                        {
                            ContentId = contentId,
                            TagId = Convert.ToInt32(item)
                        });
                    }
                }
            }

            return Task.CompletedTask;
        });
    }

    public async Task CreateContentCultures(string data, string entity, int contentId)
    {
        await _unitOfWork.ExecuteInTransactionAsync(() =>
        {
            var content = _dbContext.Contents.Single(c => c.Id == contentId);
            content.Cultures = data;
            content.UpdatedDT = DateTime.Now;
            _dbContext.Entry(content).State = EntityState.Modified;

            _dbContext.ContentInCultures
                .RemoveRange(_dbContext.ContentInCultures
                .Where(c => c.ContentId == contentId)
                .AsEnumerable());

            if (data != "" && data != null && data != string.Empty)
            {
                var Cultures = data.Split('|');

                foreach (var item in Cultures)
                {
                    if (item != null && item != "" && item != string.Empty)
                    {
                        _dbContext.ContentInCultures.Add(new ContentInCulture
                        {
                            ContentId = contentId,
                            CultureId = Convert.ToInt32(item)
                        });
                    }
                }
            }

            return Task.CompletedTask;
        });
    }

    public async Task DeleteAllContentImages(int contentId)
    {
        _dbContext.ContentImages
            .RemoveRange(_dbContext.ContentImages
            .Where(c => c.ContentId == contentId)
            .AsEnumerable());
        await _dbContext.SaveChangesAsync();
    }

    public List<ContentImage> GetAllContentImages(int contentId)
    {
        return _dbContext.ContentImages
            .Where(i => i.ContentId == contentId && !i.IsDeleted && i.IsActive)
            .ToList();
    }

    public async Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId)
    {
        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("@P_CategoryId", categoryId);
        parameters.Add("@P_ApplicationId", applicationId);

        // Short-lived connection, disposed even if the query throws, instead of a long-lived
        // field opened/closed by hand (which leaked an open connection on any exception between
        // Open() and Close(), and hid real failures behind an empty-list catch-all).
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var queryResult = await connection.QueryAsync<ContentDto>(
            "SP_ContentsInCategory", parameters, commandType: CommandType.StoredProcedure);

        return queryResult.ToList();
    }

    public async Task UpdateSectionPriority(int sectionId, int priority)
    {
        var section = await _dbContext.ContentSections.SingleAsync(cs => cs.Id == sectionId);
        section.Priority = priority;
        await _dbContext.SaveChangesAsync();
    }
}
