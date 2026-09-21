using Application.CMSRepository;

namespace Infrastructure.CMSRepository;

public class ContentQueryRepository : IContentQueryRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ContentQueryRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ContentApiDto>> GetContentByIdFull(int id, int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Contents.Where(c => c.Id == id && c.ApplicationId == applicationId && !c.IsDeleted)
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
                    Elements = s.Elements.Where(e => !e.IsDeleted).Select(e => new SectionElementApiDto
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
            .ToListAsync(cancellationToken);
    }
    public async Task<List<ContentApiDto>> GetContentByTypeId(int typeId, int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Contents.Where(c => c.TypeId == typeId && c.ApplicationId == applicationId && !c.IsDeleted && c.IsActive)
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
                // Matches the previous .Include(c => c.Sections) with no ThenInclude(Elements):
                // sections are populated, their elements are not.
                Sections = c.Sections.Where(s => !s.IsDeleted).Select(s => new ContentSectionApiDto
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
                Metadata = (c.Metadata == null || c.Metadata.IsDeleted) ? null : new ContentMetadataApiDto
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
            .ToListAsync(cancellationToken);
    }
    public async Task<BlogIndexApiDto> GetContentByTypeId(int typeId, int applicationId, int pageIndex = 1, CancellationToken cancellationToken = default)
    {
        var result = new BlogIndexApiDto();
        int skipCount = 0;
        if (pageIndex > 1)
            skipCount = 15 * (pageIndex - 1);

        result.Contents = await _dbContext.Contents
            .Where(c => c.TypeId == typeId && c.ApplicationId == applicationId && !c.IsDeleted && c.IsActive)
            .Select(c => new ContentApiDto
            {
                Id = c.Id,
                Title = c.Title,
                Abstract = c.Abstract,
                HeadLine = c.HeadLine,
                CreatedDT = c.CreatedDT,
                Categories = c.Categories,
                Images = c.Images.Where(ci => ci.Size == 640 && !ci.IsDeleted).Select(i => new ContentImageApiDto
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
            .Skip(skipCount).Take(15).ToListAsync(cancellationToken);

        var rowsCount = await _dbContext.Contents.CountAsync(c => c.TypeId == typeId && c.ApplicationId == applicationId && !c.IsDeleted && c.IsActive, cancellationToken);
        var pageCount = rowsCount / 15;
        if ((rowsCount % 15) > 0)
            pageCount++;

        result.PagesCount = pageCount;
        result.PageIndex = pageIndex;

        return result;
    }

    // A category/tag id must belong to applicationId and be active/not-deleted before its join
    // rows are trusted — this also protects against a historical ContentInCategory/Tag row that
    // links a category/tag from a different application to this application's content.
    private Task<bool> IsCategoryOwnedByApplication(int categoryId, int applicationId, CancellationToken cancellationToken)
    {
        return _dbContext.Categories.AnyAsync(c => c.Id == categoryId && c.ApplicationId == applicationId && c.IsActive && !c.IsDeleted, cancellationToken);
    }

    public async Task<BlogIndexApiDto> GetContentByCategoryId(int categoryId, int applicationId, int pageIndex = 1, int pageSize = 40, CancellationToken cancellationToken = default)
    {
        // One-based paging: page 0 is kept as a backward-compatible alias for page 1 (the
        // first page); any page >= 2 skips (page - 1) * pageSize rows.
        if (pageSize <= 0 || pageSize > 200)
            pageSize = 40;
        var normalizedPage = pageIndex <= 1 ? 1 : pageIndex;
        var skipCount = (normalizedPage - 1) * pageSize;

        var result = new BlogIndexApiDto();

        // Missing, deleted, or wrong-application category: same empty result as "no content
        // matched" — never distinguishable from a category that just has no content.
        if (!await IsCategoryOwnedByApplication(categoryId, applicationId, cancellationToken))
        {
            result.Contents = new List<ContentApiDto>();
            result.PageIndex = normalizedPage;
            result.PagesCount = 0;
            return result;
        }

        var query = from contentCategory in _dbContext.ContentInCategories
                    join content in _dbContext.Contents on contentCategory.ContentId equals content.Id
                    where contentCategory.CategoryId == categoryId
                          && content.ApplicationId == applicationId
                          && content.IsActive == true
                          && content.IsDeleted == false
                    orderby contentCategory.CreatedDt descending, contentCategory.Id descending
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

        var totalCount = await query.CountAsync(cancellationToken);

        var _result = await query
            .Skip(skipCount)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var contentIds = _result.Select(r => r.ContentId).ToList();
        var imagesByContentId = (await _dbContext.ContentImages
            .Where(i => contentIds.Contains(i.ContentId) && (i.Size == 640 || i.Size == 430 || i.Size == 860) && !i.IsDeleted)
            .ToListAsync(cancellationToken))
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
        result.PageIndex = normalizedPage;
        result.PagesCount = (int)Math.Ceiling(totalCount / (double)pageSize);

        return result;
    }

    public async Task<List<ContentApiDto>> GetContentInCategoryAsBox(int categoryId, int applicationId, CancellationToken cancellationToken = default)
    {
        // Missing, deleted, or wrong-application category: same empty result as "no content
        // matched" — never distinguishable from a category that just has no content.
        if (!await IsCategoryOwnedByApplication(categoryId, applicationId, cancellationToken))
            return new List<ContentApiDto>();

        var query = (from ccc in _dbContext.ContentInCategories
                     join cc in _dbContext.Contents on ccc.ContentId equals cc.Id
                     where cc.IsDeleted == false && cc.IsActive == true && ccc.CategoryId == categoryId
                           && cc.ApplicationId == applicationId
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

        var result = await query.ToListAsync(cancellationToken);

        var contentIds = result.Select(r => r.ContentId).ToList();
        var imagesByContentId = (await _dbContext.ContentImages
            .Where(i => contentIds.Contains(i.ContentId) && i.Size == 640 && !i.IsDeleted)
            .ToListAsync(cancellationToken))
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

    public async Task<BlogIndexApiDto> GetContentByCategoryIdByDate(int categoryId, int applicationId, DateTime startDate, DateTime endDate, int pageIndex = 1, CancellationToken cancellationToken = default)
    {
        var result = new BlogIndexApiDto();
        int skipCount = 0;
        if (pageIndex > 1)
            skipCount = 15 * (pageIndex - 1);

        // Missing, deleted, or wrong-application category: same empty result as "no content
        // matched" — never distinguishable from a category that just has no content.
        if (!await IsCategoryOwnedByApplication(categoryId, applicationId, cancellationToken))
        {
            result.Contents = new List<ContentApiDto>();
            result.PageIndex = pageIndex;
            result.PagesCount = 0;
            return result;
        }

        // Was: c.Categories.Contains(categoryId.ToString()), a substring match that also matched e.g.
        // category "1" against a content tagged "11". Now uses the ContentInCategories join table,
        // which matches on the exact category relation instead.
        var categoryFilter = _dbContext.ContentInCategories.Where(cc => cc.CategoryId == categoryId).Select(cc => cc.ContentId);

        result.Contents = await _dbContext.Contents
            .Where(c => categoryFilter.Contains(c.Id) && c.ApplicationId == applicationId && !c.IsDeleted && c.IsActive
                && c.CreatedDT > startDate && c.CreatedDT < endDate)
            .Select(c => new ContentApiDto
            {
                Id = c.Id,
                Title = c.Title,
                Abstract = c.Abstract,
                HeadLine = c.HeadLine,
                CreatedDT = c.CreatedDT,
                Categories = c.Categories,
                Images = c.Images.Where(ci => ci.Size == 640 && !ci.IsDeleted).Select(i => new ContentImageApiDto
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
            .Skip(skipCount).Take(15).ToListAsync(cancellationToken);

        var rowsCount = await _dbContext.Contents
            .CountAsync(c => categoryFilter.Contains(c.Id) && c.ApplicationId == applicationId && !c.IsDeleted && c.IsActive
                && c.CreatedDT > startDate && c.CreatedDT < endDate, cancellationToken);
        var pageCount = rowsCount / 15;
        if ((rowsCount % 15) > 0)
            pageCount++;

        result.PagesCount = pageCount;
        result.PageIndex = pageIndex;

        return result;
    }

    public async Task<List<Content>> List(int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Contents
            .Where(c => !c.IsDeleted && c.ApplicationId == applicationId)
            .OrderByDescending(c => c.CreatedDT).ToListAsync(cancellationToken);
    }

    public async Task<List<Content>> OurBlogBoxList(int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Contents
            .Where(c => !c.IsDeleted && c.ApplicationId == applicationId)
            .OrderByDescending(c => c.CreatedDT).Take(3).ToListAsync(cancellationToken);
    }

    public async Task<List<Content>> List(int applicationId, int pageIndex, CancellationToken cancellationToken = default)
    {
        // Was: Skip/Take ran before the Where filter, so pagination was computed over every
        // application's content and only filtered down afterward — content from other
        // applications could fill (or empty out) the requested page.
        return await _dbContext.Contents
            .Where(c => !c.IsDeleted && c.ApplicationId == applicationId)
            .OrderByDescending(c => c.CreatedDT)
            .Skip(pageIndex * 20).Take(20)
            .ToListAsync(cancellationToken);
    }

    public Task<int> ContentCount(int applicationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Contents
            .CountAsync(c => !c.IsDeleted && c.ApplicationId == applicationId, cancellationToken);
    }

    public async Task<Content> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Contents.AsNoTracking()
            .SingleAsync(c => c.Id == id && c.ApplicationId == applicationId && !c.IsDeleted, cancellationToken);
    }

    public async Task<ContentSection> GetSectionForApplication(int sectionId, int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentSections.AsNoTracking()
            .SingleAsync(s => s.Id == sectionId && !s.IsDeleted
                && s.Content.ApplicationId == applicationId && !s.Content.IsDeleted, cancellationToken);
    }

    public async Task<SectionElement> GetElementForApplication(int elementId, int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SectionElements.AsNoTracking()
            .SingleAsync(e => e.Id == elementId && !e.IsDeleted
                && !e.Section.IsDeleted
                && e.Section.Content.ApplicationId == applicationId && !e.Section.Content.IsDeleted, cancellationToken);
    }

    public async Task<ContentMetadata> GetContentMetadataForApplication(int metadataId, int applicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentMetadatas.AsNoTracking()
            .SingleAsync(m => m.Id == metadataId
                && m.Content.ApplicationId == applicationId && !m.Content.IsDeleted, cancellationToken);
    }

    public Task<List<ContentSection>> GetContentSections(int contentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ContentSections
            .Where(s => !s.IsDeleted && s.ContentId == contentId)
            .OrderBy(s => s.Priority).ToListAsync(cancellationToken);
    }

    public Task<List<SectionElement>> GetSectionElements(List<int> sectionIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.SectionElements
            .Where(e => !e.IsDeleted && e.IsActive && sectionIds.Contains(e.SectionId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<SectionElement>> GetSectionElements(int sectionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SectionElements
            .Where(e => !e.IsDeleted && e.IsActive && e.SectionId == sectionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContentMetadata> GetContentMetadata(int contentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentMetadatas.SingleOrDefaultAsync(m => m.ContentId == contentId && !m.IsDeleted, cancellationToken)
            ?? new ContentMetadata();
    }

    public Task<List<ContentImage>> GetAllContentImages(int contentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ContentImages
            .Where(i => i.ContentId == contentId && !i.IsDeleted && i.IsActive)
            .ToListAsync(cancellationToken);
    }
}
