using System.Text.Json;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Application.Contracts.CMSApi;
using Xunit;

namespace Core.Tests.CMSRepository;

// Covers the six public content API endpoints in Web/Areas/Api/ContentController.cs.
// These tests exist to (1) lock in the JSON-relevant shape of the new DTOs against the shape
// the old raw-entity responses actually produced, and (2) guard against regressions in the two
// behaviors this pass changed on purpose: GetContent/{id} and the no-page GetContentByTypeId no
// longer throw on serialization, and category filtering no longer substring-matches.
public class ContentApiReadTests
{
    private static ContentRepository CreateRepository(ApplicationDbContext context)
    {
        return new ContentRepository(context, TestConfiguration.Create());
    }

    // Default System.Text.Json options — the app registers no custom JSON configuration
    // (Program.cs calls AddMvc() with no AddNewtonsoftJson()/ConfigureHttpJsonOptions), so this
    // matches what ASP.NET Core actually uses to serialize controller responses today.
    private static readonly JsonSerializerOptions ProdLikeJsonOptions = new();

    private static int SeedFullContent(ApplicationDbContext context, int typeId = 1000, string categories = "1", int applicationId = 1)
    {
        var content = new Content
        {
            ApplicationId = applicationId,
            TypeId = typeId,
            Title = "Sample Content",
            HeadLine = "Sample Headline",
            Abstract = "Sample Abstract",
            Description = "Sample Description",
            Categories = categories,
            IsActive = true,
            CreatedDT = DateTime.Now
        };
        context.Contents.Add(content);
        context.SaveChanges();

        context.ContentImages.Add(new ContentImage { ContentId = content.Id, ImageFileName = "hero-640.jpg", Size = 640, IsActive = true });
        context.ContentImages.Add(new ContentImage { ContentId = content.Id, ImageFileName = "hero-1024.jpg", Size = 1024, IsActive = true });
        context.ContentMetadatas.Add(new ContentMetadata { ContentId = content.Id, Title = "Meta Title", Author = "Meta Author" });

        var section = new ContentSection { ContentId = content.Id, Priority = 1 };
        context.ContentSections.Add(section);
        context.SaveChanges();

        context.SectionElements.Add(new SectionElement { SectionId = section.Id, ElementType = 1000, TinyText = "Hello", IsActive = true });
        context.SaveChanges();

        return content.Id;
    }

    // Category-based reads now require the category itself to belong to applicationId and be
    // active/not deleted before its join rows are trusted.
    private static void SeedCategory(ApplicationDbContext context, int categoryId, int applicationId = 1)
    {
        if (!context.Categories.Any(c => c.Id == categoryId))
            context.Categories.Add(new Category { Id = categoryId, ApplicationId = applicationId, Title = $"Category {categoryId}", IsActive = true });
        context.SaveChanges();
    }

    // ---- GetContent/{id}  (backed by GetContentByIdFull) ----

    [Fact]
    public void GetContentByIdFull_WithSectionsAndImages_SerializesWithoutThrowing()
    {
        // Regression guard: the old raw-Content response threw JsonException ("A possible object
        // cycle was detected") for any content with a section or image, because EF's relationship
        // fixup wires ContentSection.Content / ContentImage.Content back to the same tracked
        // instance and System.Text.Json doesn't honor the Newtonsoft [JsonIgnore] on those
        // back-references. ContentApiDto carries no back-references, so this must now succeed.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context);

        var repository = CreateRepository(context);
        var result = repository.GetContentByIdFull(contentId, applicationId: 1);

        var json = JsonSerializer.Serialize(result, ProdLikeJsonOptions);
        Assert.NotEmpty(json);
    }

    [Fact]
    public void GetContentByIdFull_PopulatesNestedSectionsElementsAndImages()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context);

        var repository = CreateRepository(context);
        var result = repository.GetContentByIdFull(contentId, applicationId: 1);

        var dto = Assert.Single(result);
        Assert.Equal("Sample Content", dto.Title);
        Assert.Equal(2, dto.Images.Count); // unfiltered by size for this endpoint

        var section = Assert.Single(dto.Sections);
        Assert.Equal(1, section.Priority);
        var element = Assert.Single(section.Elements);
        Assert.Equal("Hello", element.TinyText);

        // Matches current behavior: GetContentByIdFull never includes Metadata.
        Assert.Null(dto.Metadata);
    }

    [Fact]
    public void GetContentByIdFull_ExcludesDeletedImagesAndSections()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context);
        context.ContentImages.Add(new ContentImage { ContentId = contentId, ImageFileName = "deleted.jpg", Size = 640, IsDeleted = true });
        context.ContentSections.Add(new ContentSection { ContentId = contentId, Priority = 2, IsDeleted = true });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentByIdFull(contentId, applicationId: 1));

        Assert.DoesNotContain(dto.Images, i => i.ImageFileName == "deleted.jpg");
        Assert.Single(dto.Sections);
    }

    [Fact]
    public void GetContentByIdFull_ExcludesDeletedSectionElements()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context);
        var section = context.ContentSections.Single(s => s.ContentId == contentId);
        context.SectionElements.Add(new SectionElement { SectionId = section.Id, ElementType = 1000, TinyText = "Deleted", IsDeleted = true });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentByIdFull(contentId, applicationId: 1));

        var resultSection = Assert.Single(dto.Sections);
        var element = Assert.Single(resultSection.Elements);
        Assert.Equal("Hello", element.TinyText);
    }

    [Fact]
    public void GetContentByIdFull_NotFound_ReturnsEmptyList()
    {
        // The controller relies on this: Ok(content[0]) when found, Ok(content) — an empty
        // array — when not found. That branch must keep working, so the method must keep
        // returning a (possibly empty) list rather than a nullable single item.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var repository = CreateRepository(context);
        var result = repository.GetContentByIdFull(999, applicationId: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void GetContentByIdFull_ContentBelongsToDifferentApplication_ReturnsEmptyList()
    {
        // Matching content id, wrong application: must be indistinguishable from a missing id.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, applicationId: 2);

        var repository = CreateRepository(context);
        var result = repository.GetContentByIdFull(contentId, applicationId: 1);

        Assert.Empty(result);
    }

    // ---- GetContentByTypeId/{typeId}  (no page) ----

    [Fact]
    public void GetContentByTypeId_NoPage_SerializesWithoutThrowing_AndPopulatesMetadata()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedFullContent(context, typeId: 2000);

        var repository = CreateRepository(context);
        var result = repository.GetContentByTypeId(2000, applicationId: 1);

        var json = JsonSerializer.Serialize(result, ProdLikeJsonOptions);
        Assert.NotEmpty(json);

        var dto = Assert.Single(result);
        Assert.NotNull(dto.Metadata); // this endpoint does .Include(c => c.Metadata) today
        Assert.Equal("Meta Title", dto.Metadata.Title);

        // Matches current behavior: this endpoint includes Sections but never ThenIncludes
        // Elements, so sections are present but their Elements are not populated.
        var section = Assert.Single(dto.Sections);
        Assert.Null(section.Elements);
    }

    [Fact]
    public void GetContentByTypeId_NoPage_ExcludesDeletedImages()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 2001);
        context.ContentImages.Add(new ContentImage { ContentId = contentId, ImageFileName = "deleted.jpg", Size = 640, IsDeleted = true });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentByTypeId(2001, applicationId: 1));

        Assert.DoesNotContain(dto.Images, i => i.ImageFileName == "deleted.jpg");
    }

    [Fact]
    public void GetContentByTypeId_NoPage_ExcludesDeletedSections()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 2003);
        context.ContentSections.Add(new ContentSection { ContentId = contentId, Priority = 2, IsDeleted = true });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentByTypeId(2003, applicationId: 1));

        Assert.Single(dto.Sections);
    }

    [Fact]
    public void GetContentByTypeId_NoPage_ExcludesDeletedMetadata()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 2004);
        var metadata = context.ContentMetadatas.Single(m => m.ContentId == contentId);
        metadata.IsDeleted = true;
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentByTypeId(2004, applicationId: 1));

        Assert.Null(dto.Metadata);
    }

    [Fact]
    public void GetContentByTypeId_NoPage_MatchingTypeIdDifferentApplication_ExcludesOtherApplication()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedFullContent(context, typeId: 2002, applicationId: 1);
        SeedFullContent(context, typeId: 2002, applicationId: 2);

        var repository = CreateRepository(context);
        var result = repository.GetContentByTypeId(2002, applicationId: 1);

        var dto = Assert.Single(result);
        Assert.Equal(1, dto.ApplicationId);
    }

    // ---- GetContentByTypeId/{typeId}/{pageIndex}  (BlogIndexApiDto) ----

    [Fact]
    public void GetContentByTypeId_Paged_ReturnsOnlyDocumentedFields()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedFullContent(context, typeId: 3000, categories: "5");

        var repository = CreateRepository(context);
        var result = repository.GetContentByTypeId(3000, applicationId: 1, pageIndex: 1);

        var dto = Assert.Single(result.Contents);
        Assert.Equal("Sample Content", dto.Title);
        Assert.Equal("5", dto.Categories);
        Assert.Single(dto.Images); // only the Size==640 image is selected here
        Assert.Equal(640, dto.Images[0].Size);

        // Matches current behavior: this projection never sets these fields.
        Assert.Null(dto.Tags);
        Assert.Null(dto.Cultures);
        Assert.Null(dto.Sections);
        Assert.Null(dto.Metadata);
        Assert.Equal(0, dto.ApplicationId);
    }

    [Fact]
    public void GetContentByTypeId_Paged_ComputesPageCount()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        for (var i = 0; i < 16; i++)
        {
            context.Contents.Add(new Content { TypeId = 4000, Title = $"Item {i}", IsActive = true, CreatedDT = DateTime.Now.AddMinutes(i) });
        }
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByTypeId(4000, applicationId: 0, pageIndex: 1);

        // 16 rows at 15/page => 2 pages (regression guard for the off-by-one page-count bug).
        Assert.Equal(2, result.PagesCount);
        Assert.Equal(15, result.Contents.Count);
    }

    [Fact]
    public void GetContentByTypeId_Paged_ExcludesDeletedImages()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 4100);
        context.ContentImages.Add(new ContentImage { ContentId = contentId, ImageFileName = "deleted-640.jpg", Size = 640, IsDeleted = true });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentByTypeId(4100, applicationId: 1, pageIndex: 1).Contents);

        Assert.DoesNotContain(dto.Images, i => i.ImageFileName == "deleted-640.jpg");
    }

    // ---- GetContentByCategoryId/{categoryId}/{pageIndex}/{pageSize} ----

    [Fact]
    public void GetContentByCategoryId_UsesJoinTable_ExactMatchOnly()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var matching = SeedFullContent(context, typeId: 5000, categories: "1");
        var nonMatching = SeedFullContent(context, typeId: 5000, categories: "11");
        SeedCategory(context, categoryId: 1);
        SeedCategory(context, categoryId: 11);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = matching, CategoryId = 1, CreatedDt = DateTime.Now });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = nonMatching, CategoryId = 11, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryId(1, applicationId: 1, pageIndex: 0, pageSize: 40);

        var dto = Assert.Single(result.Contents);
        Assert.Equal(matching, dto.Id);
    }

    [Fact]
    public void GetContentByCategoryId_SameCategoryIdDifferentApplication_ExcludesOtherApplication()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var ownApp = SeedFullContent(context, typeId: 5050, categories: "1", applicationId: 1);
        var otherApp = SeedFullContent(context, typeId: 5050, categories: "1", applicationId: 2);
        SeedCategory(context, categoryId: 50, applicationId: 1);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = ownApp, CategoryId = 50, CreatedDt = DateTime.Now });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = otherApp, CategoryId = 50, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryId(50, applicationId: 1, pageIndex: 0, pageSize: 40);

        var dto = Assert.Single(result.Contents);
        Assert.Equal(ownApp, dto.Id);
    }

    [Fact]
    public void GetContentByCategoryId_ExcludesDeletedImages()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 5060, categories: "1");
        SeedCategory(context, categoryId: 60);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = contentId, CategoryId = 60, CreatedDt = DateTime.Now });
        context.ContentImages.Add(new ContentImage { ContentId = contentId, ImageFileName = "deleted-640.jpg", Size = 640, IsDeleted = true });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentByCategoryId(60, applicationId: 1, pageIndex: 0, pageSize: 40).Contents);

        Assert.DoesNotContain(dto.Images, i => i.ImageFileName == "deleted-640.jpg");
    }

    [Fact]
    public void GetContentByCategoryId_PagesCountAndPageIndex_AreComputed()
    {
        // GetContentByCategoryId now normalizes to one-based paging and reports real pagination
        // metadata (see ContentRepositoryTests for the full page 0 / page 1 / page 2 contract).
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 5100, categories: "9");
        SeedCategory(context, categoryId: 9);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = contentId, CategoryId = 9, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryId(9, applicationId: 1);

        Assert.Equal(1, result.PagesCount);
        Assert.Equal(1, result.PageIndex);
    }

    [Fact]
    public void GetContentByCategoryId_SoftDeletedCategory_ReturnsEmptyResult()
    {
        // A deleted category must produce the same empty result as a missing one — never
        // distinguishable, even though its join rows still technically exist.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 5070, categories: "1");
        context.Categories.Add(new Category { Id = 70, ApplicationId = 1, Title = "Deleted Category", IsActive = true, IsDeleted = true });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = contentId, CategoryId = 70, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryId(70, applicationId: 1);

        Assert.Empty(result.Contents);
        Assert.Equal(0, result.PagesCount);
    }

    [Fact]
    public void GetContentByCategoryId_InactiveCategory_ReturnsEmptyResult()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 5071, categories: "1");
        context.Categories.Add(new Category { Id = 71, ApplicationId = 1, Title = "Inactive Category", IsActive = false });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = contentId, CategoryId = 71, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryId(71, applicationId: 1);

        Assert.Empty(result.Contents);
    }

    [Fact]
    public void GetContentByCategoryId_MalformedCrossApplicationRelation_ReturnsEmptyResult()
    {
        // Historical/corrupt data: a ContentInCategory row links a category that belongs to a
        // different application to this application's own content. The category ownership check
        // must reject this regardless of what the join row says.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var ownContentId = SeedFullContent(context, typeId: 5080, categories: "1", applicationId: 1);
        context.Categories.Add(new Category { Id = 80, ApplicationId = 2, Title = "Other App Category", IsActive = true });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = ownContentId, CategoryId = 80, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryId(80, applicationId: 1);

        Assert.Empty(result.Contents);
    }

    // ---- GetContentByCategoryIdByDate/{categoryId}/{startDate}/{endDate}/{pageIndex} ----

    [Fact]
    public void GetContentByCategoryIdByDate_CategoryOneDoesNotMatchCategoryEleven()
    {
        // The exact bug named in the task: Categories.Contains("1") used to also match a content
        // item whose category string was "11". Now backed by the ContentInCategories join table.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var wronglyMatchedBefore = SeedFullContent(context, typeId: 6000, categories: "11");
        SeedCategory(context, categoryId: 1);
        SeedCategory(context, categoryId: 11);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = wronglyMatchedBefore, CategoryId = 11, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryIdByDate(1, applicationId: 1, DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), pageIndex: 1);

        Assert.Empty(result.Contents);
    }

    [Fact]
    public void GetContentByCategoryIdByDate_FiltersByDateRangeAndReturnsExactMatch()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var inRange = new Content { ApplicationId = 1, TypeId = 6100, Title = "In range", Categories = "7", IsActive = true, CreatedDT = DateTime.Now };
        var outOfRange = new Content { ApplicationId = 1, TypeId = 6100, Title = "Out of range", Categories = "7", IsActive = true, CreatedDT = DateTime.Now.AddDays(-30) };
        context.Contents.AddRange(inRange, outOfRange);
        context.SaveChanges();
        SeedCategory(context, categoryId: 7);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = inRange.Id, CategoryId = 7, CreatedDt = DateTime.Now });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = outOfRange.Id, CategoryId = 7, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryIdByDate(7, applicationId: 1, DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), pageIndex: 1);

        var dto = Assert.Single(result.Contents);
        Assert.Equal("In range", dto.Title);
    }

    [Fact]
    public void GetContentByCategoryIdByDate_SameCategoryIdDifferentApplication_ExcludesOtherApplication()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var ownApp = SeedFullContent(context, typeId: 6200, categories: "8", applicationId: 1);
        var otherApp = SeedFullContent(context, typeId: 6200, categories: "8", applicationId: 2);
        SeedCategory(context, categoryId: 8, applicationId: 1);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = ownApp, CategoryId = 8, CreatedDt = DateTime.Now });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = otherApp, CategoryId = 8, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentByCategoryIdByDate(8, applicationId: 1, DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), pageIndex: 1);

        var dto = Assert.Single(result.Contents);
        Assert.Equal(ownApp, dto.Id);
    }

    [Fact]
    public void GetContentByCategoryIdByDate_ExcludesDeletedImages()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 6300, categories: "8");
        SeedCategory(context, categoryId: 63);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = contentId, CategoryId = 63, CreatedDt = DateTime.Now });
        context.ContentImages.Add(new ContentImage { ContentId = contentId, ImageFileName = "deleted-640.jpg", Size = 640, IsDeleted = true });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentByCategoryIdByDate(63, applicationId: 1, DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1), pageIndex: 1).Contents);

        Assert.DoesNotContain(dto.Images, i => i.ImageFileName == "deleted-640.jpg");
    }

    // ---- GetContentInCategoryAsBox/{categoryId} ----

    [Fact]
    public void GetContentInCategoryAsBox_ExactMatchOnly()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var matching = SeedFullContent(context, typeId: 7000, categories: "3");
        var nonMatching = SeedFullContent(context, typeId: 7000, categories: "13");
        SeedCategory(context, categoryId: 3);
        SeedCategory(context, categoryId: 13);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = matching, CategoryId = 3, CreatedDt = DateTime.Now });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = nonMatching, CategoryId = 13, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentInCategoryAsBox(3, applicationId: 1);

        var dto = Assert.Single(result);
        Assert.Equal(matching, dto.Id);
        Assert.Single(dto.Images);
    }

    [Fact]
    public void GetContentInCategoryAsBox_SameCategoryIdDifferentApplication_ExcludesOtherApplication()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var ownApp = SeedFullContent(context, typeId: 7100, categories: "4", applicationId: 1);
        var otherApp = SeedFullContent(context, typeId: 7100, categories: "4", applicationId: 2);
        SeedCategory(context, categoryId: 40, applicationId: 1);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = ownApp, CategoryId = 40, CreatedDt = DateTime.Now });
        context.ContentInCategories.Add(new ContentInCategory { ContentId = otherApp, CategoryId = 40, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var result = repository.GetContentInCategoryAsBox(40, applicationId: 1);

        var dto = Assert.Single(result);
        Assert.Equal(ownApp, dto.Id);
    }

    [Fact]
    public void GetContentInCategoryAsBox_ExcludesDeletedImages()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var contentId = SeedFullContent(context, typeId: 7200, categories: "4");
        SeedCategory(context, categoryId: 72);
        context.ContentInCategories.Add(new ContentInCategory { ContentId = contentId, CategoryId = 72, CreatedDt = DateTime.Now });
        context.ContentImages.Add(new ContentImage { ContentId = contentId, ImageFileName = "deleted-640.jpg", Size = 640, IsDeleted = true });
        context.SaveChanges();

        var repository = CreateRepository(context);
        var dto = Assert.Single(repository.GetContentInCategoryAsBox(72, applicationId: 1));

        Assert.DoesNotContain(dto.Images, i => i.ImageFileName == "deleted-640.jpg");
    }

    // ---- Cross-endpoint JSON shape checks (Part 1 baseline) ----

    [Fact]
    public void ContentApiDto_SerializesWithPascalCasePropertyNames()
    {
        // No JsonNamingPolicy is configured anywhere in the app (verified against Program.cs),
        // so System.Text.Json falls back to the literal C# member names — same as it did for the
        // raw entity before. Locks in the exact keys a client currently depends on.
        var dto = new ContentApiDto { Id = 1, Title = "T", Categories = "1|2", Images = new(), Sections = new() };

        var json = JsonSerializer.Serialize(dto, ProdLikeJsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("Id", out _));
        Assert.True(root.TryGetProperty("Title", out _));
        Assert.True(root.TryGetProperty("Categories", out _));
        Assert.True(root.TryGetProperty("Images", out _));
        Assert.True(root.TryGetProperty("Sections", out _));
        Assert.True(root.TryGetProperty("Metadata", out _));
        Assert.False(root.TryGetProperty("id", out _)); // not camelCase
    }

    [Fact]
    public void BlogIndexApiDto_SerializesWithPaginationWrapperFields()
    {
        var dto = new BlogIndexApiDto { Contents = new(), PagesCount = 3, PageIndex = 1 };

        var json = JsonSerializer.Serialize(dto, ProdLikeJsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("Contents", out _));
        Assert.Equal(3, root.GetProperty("PagesCount").GetInt32());
        Assert.Equal(1, root.GetProperty("PageIndex").GetInt32());
    }
}
