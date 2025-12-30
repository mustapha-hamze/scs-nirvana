namespace Infrastructure.CMSRepository;

public class ContentRepository : Repository<Content>, IContentRepository
{
    // fields
    private readonly ApplicationDbContext _dbContext;
    private readonly SqlConnection _sqlConnection;

    // constructor
    public ContentRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
        _sqlConnection = new(ConnectionString);
    }

    // methods
    public List<Content> GetContentByIdFull(int id)
    {
        return _dbContext.Contents.Where(c => c.Id == id)
            .Include(c => c.Images.Where(ci => !ci.IsDeleted))
            .Include(c => c.Sections.Where(s => !s.IsDeleted).OrderBy(s => s.Priority)).ThenInclude(s => s.Elements)
            .Include(c => c.Images.Where(i => !i.IsDeleted))
            .ToList();
    }
    public List<Content> GetContentByTypeId(int typeId)
    {
        return _dbContext.Contents.Where(c => c.TypeId == typeId && !c.IsDeleted && c.IsActive)
        .Include(c => c.Images)
        .Include(c => c.Metadata)
        .Include(c => c.Sections)
        .ToList();
    }
    public BlogIndexDto GetContentByTypeId(int typeId, int pageIndex = 1)
    {
        var result = new BlogIndexDto();
        int skipCount = 0;
        if (pageIndex > 1)
            skipCount = 15 * (pageIndex - 1);

        result.Contents = _dbContext.Contents
            .Where(c => c.TypeId == typeId && !c.IsDeleted && c.IsActive)
            .Select(c => new Content
            {
                Id = c.Id,
                Title = c.Title,
                Abstract = c.Abstract,
                HeadLine = c.HeadLine,
                CreatedDT = c.CreatedDT,
                Categories = c.Categories,
                Images = c.Images.Where(ci => ci.Size == 640).ToList()
            })
            .OrderByDescending(c => c.CreatedDT)
            .Skip(skipCount).Take(15).ToList();

        var rowsCount = _dbContext.Contents.Count(c => c.TypeId == typeId && !c.IsDeleted && c.IsActive);
        var pageCount = rowsCount / 15;
        if ((rowsCount % 15) > 1)
            pageCount++;

        result.PagesCount = pageCount;
        result.PageIndex = pageIndex;

        return result;
    }

    public BlogIndexDto GetContentByCategoryId(int categoryId, int pageIndex = 1, int pageSize = 40)
    {
        var result = new BlogIndexDto();
        int skipCount = 0;
        if (pageIndex > 0)
            skipCount = pageSize * pageIndex;

        //     result.Contents = _dbContext.Contents
        //         .Where(c => c.Categories.Contains(categoryId.ToString()) && !c.IsDeleted && c.IsActive)
        //         .Select(c => new Content
        //         {
        //             Id = c.Id,
        //             Title = c.Title,
        //             Title_FA = c.Title_FA,
        //             Abstract = c.Abstract,
        //             Abstract_FA = c.Abstract_FA,
        //             HeadLine = c.HeadLine,
        //             HeadLine_FA = c.HeadLine_FA,
        //             CreatedDT = c.CreatedDT,
        //             Categories = c.Categories,
        //             Images = c.Images.Where(ci => ci.Size == 640).ToList()
        //         })
        //         .OrderByDescending(c => c.CreatedDT)
        //         .Skip(skipCount).Take(15).ToList();

        //     var rowsCount = _dbContext.Contents
        //    .Count(c => c.Categories.Contains(categoryId.ToString()) && !c.IsDeleted && c.IsActive);
        //     var pageCount = rowsCount / 15;
        //     if ((rowsCount % 15) > 1)
        //         pageCount++;

        //     result.PagesCount = pageCount;
        //     result.PageIndex = pageIndex;

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

        List<Content> contents = new();
        foreach (var item in _result)
        {
            contents.Add(new Content
            {
                Id = item.ContentId,
                Title = item.Title,
                Abstract = item.Abstract,
                HeadLine = item.HeadLine,
                Description = item.Description,
                CreatedDT = item.ContentCreatedDT,
                Images = _dbContext.ContentImages.Where(i => i.ContentId == item.ContentId && (i.Size == 640 || i.Size == 430 || i.Size == 860)).ToList()
            });
        }

        result.Contents = contents.OrderByDescending(c => c.CreatedDT).ToList();

        return result;
    }

    public List<Content> GetContentInCategoryAsBox(int categoryId)
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

        List<Content> contents = new();
        foreach (var item in result)
        {
            contents.Add(new Content
            {
                Id = item.ContentId,
                Title = item.Title,
                Abstract = item.Abstract,
                HeadLine = item.HeadLine,
                Description = item.Description,
                Images = _dbContext.ContentImages.Where(i => i.ContentId == item.ContentId && i.Size == 640).ToList()
            });
        }

        return contents;
    }

    public BlogIndexDto GetContentByCategoryIdByDate(int categoryId, DateTime startDate, DateTime endDate, int pageIndex = 1)
    {
        var result = new BlogIndexDto();
        int skipCount = 0;
        if (pageIndex > 1)
            skipCount = 15 * (pageIndex - 1);

        result.Contents = _dbContext.Contents
            .Where(c => c.Categories.Contains(categoryId.ToString()) && !c.IsDeleted && c.IsActive
                && c.CreatedDT > startDate && c.CreatedDT < endDate)
            .Select(c => new Content
            {
                Id = c.Id,
                Title = c.Title,
                Abstract = c.Abstract,
                HeadLine = c.HeadLine,
                CreatedDT = c.CreatedDT,
                Categories = c.Categories,
                Images = c.Images.Where(ci => ci.Size == 640).ToList()
            })
            .OrderByDescending(c => c.CreatedDT)
            .Skip(skipCount).Take(15).ToList();

        var rowsCount = _dbContext.Contents
       .Count(c => c.Categories.Contains(categoryId.ToString()) && !c.IsDeleted && c.IsActive);
        var pageCount = rowsCount / 15;
        if ((rowsCount % 15) > 1)
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
        var content = _dbContext.Contents.Single(c => c.Id == contentId);

        content.Categories = data;
        content.UpdatedDT = DateTime.Now;
        _dbContext.Entry(content).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();

        _dbContext.ContentInCategories
            .RemoveRange(_dbContext.ContentInCategories
            .Where(c => c.ContentId == contentId)
            .AsEnumerable());
        await _dbContext.SaveChangesAsync();

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
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
    }

    public async Task CreateContentTags(string data, string entity, int contentId)
    {
        var content = _dbContext.Contents.Single(c => c.Id == contentId);
        content.Tags = data;
        content.UpdatedDT = DateTime.Now;
        _dbContext.Entry(content).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();

        _dbContext.ContentInTags
            .RemoveRange(_dbContext.ContentInTags
            .Where(c => c.ContentId == contentId)
            .AsEnumerable());
        await _dbContext.SaveChangesAsync();

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
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
    }

    public async Task CreateContentCultures(string data, string entity, int contentId)
    {
        var content = _dbContext.Contents.Single(c => c.Id == contentId);
        content.Cultures = data;
        content.UpdatedDT = DateTime.Now;
        _dbContext.Entry(content).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();

        _dbContext.ContentInCultures
            .RemoveRange(_dbContext.ContentInCultures
            .Where(c => c.ContentId == contentId)
            .AsEnumerable());
        await _dbContext.SaveChangesAsync();

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
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
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
        try
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@P_CategoryId", categoryId);
            parameters.Add("@P_ApplicationId", applicationId);
            _sqlConnection.Open();
            var queryResult = await _sqlConnection.QueryAsync
                <ContentDto>("SP_ContentsInCategory", parameters, commandType: CommandType.StoredProcedure);
            _sqlConnection.Close();

            return queryResult.ToList();
        }
        catch (Exception)
        {
            return new List<ContentDto>();
        }
    }

    public async Task UpdateSectionPriority(int sectionId, int priority)
    {
        var section = await _dbContext.ContentSections.SingleAsync(cs => cs.Id == sectionId);
        section.Priority = priority;
        await _dbContext.SaveChangesAsync();
    }
}
