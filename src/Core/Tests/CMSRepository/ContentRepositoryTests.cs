using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.CMSRepository;

public class ContentRepositoryTests
{
    [Fact]
    public async Task CreateContentCategories_ReplacesRelations_InOneAtomicOperation()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Sample", Categories = "1|2" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        context.ContentInCategories.Add(new ContentInCategory { ContentId = content.Id, CategoryId = 1, CreatedDt = DateTime.Now });
        await context.SaveChangesAsync();

        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context);
        var repository = new ContentRepository(context, TestConfiguration.Create(), unitOfWork);

        await repository.CreateContentCategories(content.Id, new List<int> { 3, 4, 5 });

        await using var verifyContext = factory.CreateContext();
        var relations = await verifyContext.ContentInCategories.Where(c => c.ContentId == content.Id).ToListAsync();
        var updatedContent = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);

        Assert.Equal(new[] { 3, 4, 5 }, relations.Select(r => r.CategoryId).OrderBy(id => id));
        Assert.Equal("3|4|5", updatedContent.Categories);
    }

    [Fact]
    public async Task CreateContentCategories_DuplicateIdsInInput_ViolatesUniqueConstraint()
    {
        // Defense in depth: the composite-unique index on (ContentId, CategoryId) added to the
        // EF model means even a caller that bypasses ContentServices' own de-duplication can't
        // land two rows for the same (content, category) pair.
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Sample" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context);
        var repository = new ContentRepository(context, TestConfiguration.Create(), unitOfWork);

        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.CreateContentCategories(content.Id, new List<int> { 3, 3 }));
    }

    [Fact]
    public async Task CreateContentCategories_EmptyData_RemovesExistingRelationsAndClearsField()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Sample", Categories = "1" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        context.ContentInCategories.Add(new ContentInCategory { ContentId = content.Id, CategoryId = 1, CreatedDt = DateTime.Now });
        await context.SaveChangesAsync();

        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context);
        var repository = new ContentRepository(context, TestConfiguration.Create(), unitOfWork);

        await repository.CreateContentCategories(content.Id, new List<int>());

        await using var verifyContext = factory.CreateContext();
        var relations = await verifyContext.ContentInCategories.Where(c => c.ContentId == content.Id).ToListAsync();
        var updatedContent = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);

        Assert.Empty(relations);
        Assert.Equal("", updatedContent.Categories);
    }

    [Fact]
    public void List_Paged_OtherApplicationsContentCannotFillOrEmptyThePage()
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

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        var firstPage = repository.List(applicationId: 1, pageIndex: 0);
        var secondPage = repository.List(applicationId: 1, pageIndex: 1);

        Assert.Equal(20, firstPage.Count);
        Assert.All(firstPage, c => Assert.Equal(1, c.ApplicationId));
        Assert.Equal(5, secondPage.Count);
        Assert.All(secondPage, c => Assert.Equal(1, c.ApplicationId));
    }

    [Fact]
    public async Task GetContentsInCategory_ConnectionFailure_ThrowsInsteadOfSwallowingAndReturningEmptyList()
    {
        // Regression guard: this used to open a long-lived SqlConnection field and swallow every
        // exception behind `catch (Exception) { return new List<ContentDto>(); }`, so a genuine
        // failure (bad connection string, unreachable server, broken stored procedure) was
        // indistinguishable from "no rows found". It must now propagate.
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context);
        var repository = new ContentRepository(context, TestConfiguration.CreateUnreachable(), unitOfWork);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetContentsInCategory(categoryId: 1, applicationId: 1));
    }

    [Fact]
    public async Task UpdateFarsiContent_SetsFarsiContent_AndPreservesOtherFields()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Original Title", Abstract = "Original Abstract", FarsiContent = null };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        await repository.UpdateFarsiContent(content.Id, "{\"title\":\"ترجمه\"}");
        await context.SaveChangesAsync();

        await using var verifyContext = factory.CreateContext();
        var updated = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);
        Assert.Equal("{\"title\":\"ترجمه\"}", updated.FarsiContent);
        Assert.Equal("Original Title", updated.Title);
        Assert.Equal("Original Abstract", updated.Abstract);
    }

    [Fact]
    public async Task UpdateFarsiContent_WhenContentAlreadyTrackedInSameContext_DoesNotThrow()
    {
        // The exact bug this method exists to fix: IContentProvider.GetContentForTranslate loads
        // Content into the change tracker (no AsNoTracking). The old UpdateTranslate then called
        // the generic Repository<T>.GetById (AsNoTracking) for the same id and passed that second,
        // untracked instance to .Update() — EF refuses to track two instances with the same key,
        // and throws InvalidOperationException. UpdateFarsiContent must not hit that conflict.
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Sample" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        // Simulate GetContentForTranslate: a tracked read of the same entity, in the same context,
        // before the update call.
        var alreadyTracked = await context.Contents.Include(c => c.Images).SingleAsync(c => c.Id == content.Id);

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        var exception = await Record.ExceptionAsync(() => repository.UpdateFarsiContent(content.Id, "translated"));

        Assert.Null(exception);
        Assert.Equal("translated", alreadyTracked.FarsiContent); // same tracked instance, mutated in place
    }

    [Fact]
    public async Task ActivateTranslatedContent_SetsFarsiContentAndActivates()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Sample", IsActive = false };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        await repository.ActivateTranslatedContent(content.Id, "translated");
        await context.SaveChangesAsync();

        await using var verifyContext = factory.CreateContext();
        var updated = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);
        Assert.Equal("translated", updated.FarsiContent);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task ActivateTranslatedContent_WhenContentAlreadyTrackedInSameContext_DoesNotThrow()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Sample", IsActive = false };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var alreadyTracked = await context.Contents.SingleAsync(c => c.Id == content.Id);

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        var exception = await Record.ExceptionAsync(() => repository.ActivateTranslatedContent(content.Id, "translated"));

        Assert.Null(exception);
        Assert.True(alreadyTracked.IsActive);
    }

    [Fact]
    public async Task GetByIdForApplication_SameApplication_ReturnsContent()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { ApplicationId = 1, TypeId = 1000, Title = "Sample" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

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

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(content.Id, 2));
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

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(content.Id, 0));
    }

    private static void SeedCategoryContents(Infrastructure.Data.ApplicationDbContext context, int categoryId, int count)
    {
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
    public void GetContentByCategoryId_Page0_ReturnsFirstPage()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        var result = repository.GetContentByCategoryId(categoryId: 5, pageIndex: 0, pageSize: 3);

        Assert.Equal(3, result.Contents.Count);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(2, result.PagesCount);
    }

    [Fact]
    public void GetContentByCategoryId_DefaultPage_ReturnsFirstPage()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        var page0 = repository.GetContentByCategoryId(categoryId: 5, pageIndex: 0, pageSize: 3);
        var page1 = repository.GetContentByCategoryId(categoryId: 5, pageSize: 3); // pageIndex defaults to 1

        Assert.Equal(page0.Contents.Select(c => c.Id), page1.Contents.Select(c => c.Id));
        Assert.Equal(1, page1.PageIndex);
    }

    [Fact]
    public void GetContentByCategoryId_Page2_SkipsFirstPage()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        var firstPage = repository.GetContentByCategoryId(categoryId: 5, pageIndex: 1, pageSize: 3);
        var secondPage = repository.GetContentByCategoryId(categoryId: 5, pageIndex: 2, pageSize: 3);

        Assert.Equal(3, firstPage.Contents.Count);
        Assert.Equal(2, secondPage.Contents.Count);
        Assert.Equal(2, secondPage.PageIndex);
        Assert.Empty(firstPage.Contents.Select(c => c.Id).Intersect(secondPage.Contents.Select(c => c.Id)));
    }

    [Fact]
    public void GetContentByCategoryId_InvalidPageSize_FallsBackToDefault()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        SeedCategoryContents(context, categoryId: 5, count: 5);

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        var result = repository.GetContentByCategoryId(categoryId: 5, pageIndex: 1, pageSize: 0);

        Assert.Equal(5, result.Contents.Count); // all 5 fit within the 40-item default page size
        Assert.Equal(1, result.PagesCount);
    }

    [Fact]
    public void GetContentByCategoryId_EmptyResult_ReturnsZeroPagesAndNoContents()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var repository = new ContentRepository(context, TestConfiguration.Create(), new Infrastructure.UnitOfWork.UnitOfWork(context));

        var result = repository.GetContentByCategoryId(categoryId: 999, pageIndex: 1, pageSize: 10);

        Assert.Empty(result.Contents);
        Assert.Equal(0, result.PagesCount);
        Assert.Equal(1, result.PageIndex);
    }
}
