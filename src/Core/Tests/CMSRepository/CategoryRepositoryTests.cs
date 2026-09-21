using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Xunit;

namespace Core.Tests.CMSRepository;

public class CategoryRepositoryTests
{
    [Fact]
    public async Task GetByIdForApplication_SameApplication_ReturnsCategory()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var category = new Category { ApplicationId = 1, Title = "Sample" };
        context.Categories.Add(category);
        context.SaveChanges();

        var repository = new CategoryRepository(context);

        var result = await repository.GetByIdForApplication(category.Id, 1);

        Assert.Equal(category.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdForApplication_DifferentApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var category = new Category { ApplicationId = 1, Title = "Sample" };
        context.Categories.Add(category);
        context.SaveChanges();

        var repository = new CategoryRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(category.Id, 2));
    }

    [Fact]
    public async Task GetByIdForApplication_SoftDeleted_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var category = new Category { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Categories.Add(category);
        context.SaveChanges();

        var repository = new CategoryRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(category.Id, 1));
    }
}
