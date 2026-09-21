using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.CMSRepository;

public class ContentRelationRepositoryTests
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

        // The repository only stages the replace; the Application layer is what wraps it in a
        // transaction (exactly how ContentServices.CreateContentCategories composes it).
        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.UnitOfWork.UnitOfWork>.Instance);
        var repository = new ContentRelationRepository(context);

        await unitOfWork.ExecuteInTransactionAsync(() => repository.CreateContentCategories(content.Id, new List<int> { 3, 4, 5 }));

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

        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.UnitOfWork.UnitOfWork>.Instance);
        var repository = new ContentRelationRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(
            () => unitOfWork.ExecuteInTransactionAsync(() => repository.CreateContentCategories(content.Id, new List<int> { 3, 3 })));
    }

    [Fact]
    public async Task CreateContentCategories_FailurePartwayThrough_RollsBackCompatStringAndRelations()
    {
        // The whole replace (compat string + join rows) runs in one Application-owned
        // transaction: a failure on the last insert must leave both the string and the rows
        // exactly as they were, not a half-applied mix of the old rows and the new string (or
        // vice versa). This is now enforced by the caller composing the transaction around the
        // repository's staging-only method, not by the repository itself.
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Sample", Categories = "1" };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        context.ContentInCategories.Add(new ContentInCategory { ContentId = content.Id, CategoryId = 1, CreatedDt = DateTime.Now });
        await context.SaveChangesAsync();

        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.UnitOfWork.UnitOfWork>.Instance);
        var repository = new ContentRelationRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(
            () => unitOfWork.ExecuteInTransactionAsync(() => repository.CreateContentCategories(content.Id, new List<int> { 3, 3 })));

        await using var verifyContext = factory.CreateContext();
        var relations = await verifyContext.ContentInCategories.Where(c => c.ContentId == content.Id).ToListAsync();
        var unchangedContent = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);

        Assert.Equal("1", unchangedContent.Categories);
        var relation = Assert.Single(relations);
        Assert.Equal(1, relation.CategoryId);
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

        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.UnitOfWork.UnitOfWork>.Instance);
        var repository = new ContentRelationRepository(context);

        await unitOfWork.ExecuteInTransactionAsync(() => repository.CreateContentCategories(content.Id, new List<int>()));

        await using var verifyContext = factory.CreateContext();
        var relations = await verifyContext.ContentInCategories.Where(c => c.ContentId == content.Id).ToListAsync();
        var updatedContent = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);

        Assert.Empty(relations);
        Assert.Equal("", updatedContent.Categories);
    }
}
