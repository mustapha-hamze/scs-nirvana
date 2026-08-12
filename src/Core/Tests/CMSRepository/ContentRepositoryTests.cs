using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Core.Tests.CMSRepository;

public class ContentRepositoryTests
{
    private static IConfiguration CreateConfiguration()
    {
        var section = new Mock<IConfigurationSection>();
        section.Setup(s => s["DefaultConnection"]).Returns("Data Source=test;Initial Catalog=test;Integrated Security=true;");

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c.GetSection("ConnectionStrings")).Returns(section.Object);

        return configuration.Object;
    }

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
        var repository = new ContentRepository(context, CreateConfiguration(), unitOfWork);

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
        var repository = new ContentRepository(context, CreateConfiguration(), unitOfWork);

        await repository.CreateContentCategories("", "category", content.Id);

        await using var verifyContext = factory.CreateContext();
        var relations = await verifyContext.ContentInCategories.Where(c => c.ContentId == content.Id).ToListAsync();
        var updatedContent = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);

        Assert.Empty(relations);
        Assert.Equal("", updatedContent.Categories);
    }
}
