using Core.Tests.TestSupport;
using Domains.Entities.General;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.Repository;

public class RepositoryTests
{
    [Fact]
    public async Task Create_SetsAuditFieldsAndPersists()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();
        var repository = new Repository<Culture>(context);

        var created = await repository.Create(new Culture { ApplicationId = 1, Title = "English", Key = "en" });

        Assert.NotEqual(0, created.Id);
        Assert.False(created.IsDeleted);

        await using var verifyContext = factory.CreateContext();
        var stored = await verifyContext.Set<Culture>().SingleAsync(c => c.Id == created.Id);
        Assert.Equal("English", stored.Title);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Delete_IsSoftDelete_RowStillExistsButMarkedDeleted()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();
        var repository = new Repository<Culture>(context);
        var created = await repository.Create(new Culture { ApplicationId = 1, Title = "Farsi", Key = "fa" });

        await repository.Delete(created.Id);

        await using var verifyContext = factory.CreateContext();
        var stored = await verifyContext.Set<Culture>().SingleOrDefaultAsync(c => c.Id == created.Id);

        Assert.NotNull(stored); // row was not physically removed
        Assert.True(stored.IsDeleted);
    }
}
