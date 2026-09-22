using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.CMSRepository;

public class ContentCommandRepositoryTests
{
    [Fact]
    public async Task UpdateFarsiContent_SetsFarsiContent_AndPreservesOtherFields()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var content = new Content { TypeId = 1000, Title = "Original Title", Abstract = "Original Abstract", FarsiContent = null };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var repository = new ContentCommandRepository(context);

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

        var repository = new ContentCommandRepository(context);

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

        var repository = new ContentCommandRepository(context);

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

        var repository = new ContentCommandRepository(context);

        var exception = await Record.ExceptionAsync(() => repository.ActivateTranslatedContent(content.Id, "translated"));

        Assert.Null(exception);
        Assert.True(alreadyTracked.IsActive);
    }
}
