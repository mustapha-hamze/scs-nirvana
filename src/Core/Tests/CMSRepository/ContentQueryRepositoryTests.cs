using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.CMSRepository;

public class ContentQueryRepositoryTests
{
    [Fact]
    public async Task List_Paged_OtherApplicationsContentCannotFillOrEmptyThePage()
    {
        // Regression guard: Skip/Take used to run before the Where(ApplicationId == ...) filter,
        // so pagination was computed over every application's content and filtered afterward.
        // Seed far more "other application" content, created more recently, than would fit in a
        // single page — under the old code this would crowd out the target application entirely.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var baseTime = DateTime.Now;
        for (var i = 0; i < 30; i++)
        {
            context.Contents.Add(new Content { ApplicationId = 2, TypeId = 1000, Title = $"Other app {i}", IsActive = true, CreatedDT = baseTime.AddMinutes(100 + i) });
        }
        for (var i = 0; i < 25; i++)
        {
            context.Contents.Add(new Content { ApplicationId = 1, TypeId = 1000, Title = $"Target app {i}", IsActive = true, CreatedDT = baseTime.AddMinutes(i) });
        }
        context.SaveChanges();

        var repository = new ContentQueryRepository(context);

        var firstPage = await repository.List(applicationId: 1, pageIndex: 0);
        var secondPage = await repository.List(applicationId: 1, pageIndex: 1);

        Assert.Equal(20, firstPage.Count);
        Assert.All(firstPage, c => Assert.Equal(1, c.ApplicationId));
        Assert.Equal(5, secondPage.Count);
        Assert.All(secondPage, c => Assert.Equal(1, c.ApplicationId));
    }

    [Fact]
    public async Task GetByIdForApplication_SameApplication_ReturnsContent()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { ApplicationId = 1, TypeId = 1000, Title = "Sample" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var repository = new ContentQueryRepository(context);

        var result = await repository.GetByIdForApplication(content.Id, 1);

        Assert.Equal(content.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdForApplication_DifferentApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { ApplicationId = 1, TypeId = 1000, Title = "Sample" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var repository = new ContentQueryRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(content.Id, 2));
    }

    [Fact]
    public async Task GetByIdForApplication_SoftDeletedSameApplication_Throws()
    {
        // A deleted resource must behave as not found - same outcome as a cross-application id.
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { ApplicationId = 1, TypeId = 1000, Title = "Deleted", IsDeleted = true };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var repository = new ContentQueryRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(content.Id, 1));
    }

    [Fact]
    public async Task GetByIdForApplication_IgnoredApplicationId_DoesNotFallBackToAnyApplication()
    {
        // A caller passing applicationId: 0 (e.g. an uninitialized/ignored value) must not be
        // treated as "any application" — it must behave like any other wrong application.
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { ApplicationId = 1, TypeId = 1000, Title = "Sample" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var repository = new ContentQueryRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(content.Id, 0));
    }

    private static void SeedCategoryContents(Infrastructure.Data.ApplicationDbContext context, int categoryId, int count)
    {
        if (!context.Categories.Any(c => c.Id == categoryId))
            context.Categories.Add(new Category { Id = categoryId, ApplicationId = 1, Title = $"Category {categoryId}", IsActive = true });

        var baseTime = DateTime.Now;
        for (var i = 0; i < count; i++)
        {
            var content = new Content { ApplicationId = 1, TypeId = 1000, Title = $"Content {i}", IsActive = true, CreatedDT = baseTime.AddMinutes(i) };
            context.Contents.Add(content);
            context.SaveChanges();
            context.ContentInCategories.Add(new ContentInCategory { ContentId = content.Id, CategoryId = categoryId, CreatedDt = baseTime.AddMinutes(i) });
        }
        context.SaveChanges();
    }

    [Fact]
    public async Task GetContentByCategoryId_Page0_ReturnsFirstPage()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var repository = new ContentQueryRepository(context);

        var result = await repository.GetContentByCategoryId(categoryId: 5, applicationId: 1, pageIndex: 0, pageSize: 3);

        Assert.Equal(3, result.Contents.Count);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(2, result.PagesCount);
    }

    [Fact]
    public async Task GetContentByCategoryId_DefaultPage_ReturnsFirstPage()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var repository = new ContentQueryRepository(context);

        var page0 = await repository.GetContentByCategoryId(categoryId: 5, applicationId: 1, pageIndex: 0, pageSize: 3);
        var page1 = await repository.GetContentByCategoryId(categoryId: 5, applicationId: 1, pageSize: 3); // pageIndex defaults to 1

        Assert.Equal(page0.Contents.Select(c => c.Id), page1.Contents.Select(c => c.Id));
        Assert.Equal(1, page1.PageIndex);
    }

    [Fact]
    public async Task GetContentByCategoryId_Page2_SkipsFirstPage()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var repository = new ContentQueryRepository(context);

        var firstPage = await repository.GetContentByCategoryId(categoryId: 5, applicationId: 1, pageIndex: 1, pageSize: 3);
        var secondPage = await repository.GetContentByCategoryId(categoryId: 5, applicationId: 1, pageIndex: 2, pageSize: 3);

        Assert.Equal(3, firstPage.Contents.Count);
        Assert.Equal(2, secondPage.Contents.Count);
        Assert.Equal(2, secondPage.PageIndex);
        Assert.Empty(firstPage.Contents.Select(c => c.Id).Intersect(secondPage.Contents.Select(c => c.Id)));
    }

    [Fact]
    public async Task GetContentByCategoryId_InvalidPageSize_FallsBackToDefault()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var repository = new ContentQueryRepository(context);

        var result = await repository.GetContentByCategoryId(categoryId: 5, applicationId: 1, pageIndex: 1, pageSize: 0);

        Assert.Equal(5, result.Contents.Count); // all 5 fit within the 40-item default page size
        Assert.Equal(1, result.PagesCount);
    }

    [Fact]
    public async Task GetContentByCategoryId_EmptyResult_ReturnsZeroPagesAndNoContents()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var repository = new ContentQueryRepository(context);

        var result = await repository.GetContentByCategoryId(categoryId: 999, applicationId: 1, pageIndex: 1, pageSize: 10);

        Assert.Empty(result.Contents);
        Assert.Equal(0, result.PagesCount);
        Assert.Equal(1, result.PageIndex);
    }

    [Fact]
    public async Task GetContentByCategoryId_MatchingCategoryIdDifferentApplication_ExcludesOtherApplication()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var otherAppContent = new Content { ApplicationId = 2, TypeId = 1000, Title = "Other app", IsActive = true, CreatedDT = DateTime.Now };
        context.Contents.Add(otherAppContent);
        context.SaveChanges();
        context.ContentInCategories.Add(new ContentInCategory { ContentId = otherAppContent.Id, CategoryId = 5, CreatedDt = DateTime.Now });
        context.SaveChanges();

        var repository = new ContentQueryRepository(context);

        var result = await repository.GetContentByCategoryId(categoryId: 5, applicationId: 1, pageIndex: 1, pageSize: 40);

        Assert.Equal(5, result.Contents.Count); // the app-1 seeded rows only, not the 6th (app 2) row
        Assert.DoesNotContain(result.Contents, c => c.Id == otherAppContent.Id);
    }

    [Fact]
    public async Task GetContentMetadata_ReturnsMetadata_WhenNotDeleted()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var content = new Content { TypeId = 1000, Title = "Sample" };
        context.Contents.Add(content);
        context.SaveChanges();
        context.ContentMetadatas.Add(new ContentMetadata { ContentId = content.Id, Title = "Meta" });
        context.SaveChanges();

        var repository = new ContentQueryRepository(context);

        var result = await repository.GetContentMetadata(content.Id);

        Assert.Equal("Meta", result.Title);
    }

    [Fact]
    public async Task GetContentMetadata_SoftDeleted_ReturnsEmptyMetadata()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var content = new Content { TypeId = 1000, Title = "Sample" };
        context.Contents.Add(content);
        context.SaveChanges();
        context.ContentMetadatas.Add(new ContentMetadata { ContentId = content.Id, Title = "Meta", IsDeleted = true });
        context.SaveChanges();

        var repository = new ContentQueryRepository(context);

        var result = await repository.GetContentMetadata(content.Id);

        Assert.Equal(0, result.Id);
        Assert.Null(result.Title);
    }
}
