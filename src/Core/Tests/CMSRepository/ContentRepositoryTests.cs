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
}
