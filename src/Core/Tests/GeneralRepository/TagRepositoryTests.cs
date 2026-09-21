using Core.Tests.TestSupport;
using Domains.Entities.General;
using Infrastructure.GeneralRepository;
using Xunit;

namespace Core.Tests.GeneralRepository;

public class TagRepositoryTests
{
    [Fact]
    public async Task GetByIdForApplication_SameApplication_ReturnsTag()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var tag = new Tag { ApplicationId = 1, Title = "Sample" };
        context.Tags.Add(tag);
        context.SaveChanges();

        var repository = new TagRepository(context);

        var result = await repository.GetByIdForApplication(tag.Id, 1);

        Assert.Equal(tag.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdForApplication_DifferentApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var tag = new Tag { ApplicationId = 1, Title = "Sample" };
        context.Tags.Add(tag);
        context.SaveChanges();

        var repository = new TagRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(tag.Id, 2));
    }

    [Fact]
    public async Task GetByIdForApplication_SoftDeleted_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var tag = new Tag { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Tags.Add(tag);
        context.SaveChanges();

        var repository = new TagRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(tag.Id, 1));
    }
}
