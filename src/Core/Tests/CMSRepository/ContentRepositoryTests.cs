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

        await repository.CreateContentCategories("3|4|5", "category", content.Id);

        await using var verifyContext = factory.CreateContext();
        var relations = await verifyContext.ContentInCategories.Where(c => c.ContentId == content.Id).ToListAsync();
        var updatedContent = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);

        Assert.Equal(new[] { 3, 4, 5 }, relations.Select(r => r.CategoryId).OrderBy(id => id));
        Assert.Equal("3|4|5", updatedContent.Categories);
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

        await repository.CreateContentCategories("", "category", content.Id);

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
}
