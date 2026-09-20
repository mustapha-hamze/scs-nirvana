using Core.Tests.TestSupport;
using Domains.Entities.AccessManagement;
using Infrastructure.AccessManagerRepository;
using Xunit;

namespace Core.Tests.AccessManagerRepository;

public class SectorRepositoryTests
{
    [Fact]
    public void GetAllSector_IgnoredApplicationId_OnlyReturnsThatApplicationsSectors()
    {
        // Regression guard: GetAllSector(applicationId) used to ignore its parameter entirely
        // and return every application's sectors.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Sectors.Add(new Sector { ApplicationId = 1, Title = "App1 Sector" });
        context.Sectors.Add(new Sector { ApplicationId = 2, Title = "App2 Sector" });
        context.SaveChanges();

        var repository = new SectorRepository(context);

        var result = repository.GetAllSector(applicationId: 1);

        Assert.Single(result);
        Assert.Equal("App1 Sector", result[0].Title);
    }

    [Fact]
    public void GetAllSector_ExcludesSoftDeletedRows()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        context.Sectors.Add(new Sector { ApplicationId = 1, Title = "Active", IsDeleted = false });
        context.Sectors.Add(new Sector { ApplicationId = 1, Title = "Deleted", IsDeleted = true });
        context.SaveChanges();

        var repository = new SectorRepository(context);

        var result = repository.GetAllSector(applicationId: 1);

        Assert.Single(result);
        Assert.Equal("Active", result[0].Title);
    }

    [Fact]
    public async Task GetByIdForApplication_CrossApplicationId_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sector = new Sector { ApplicationId = 1, Title = "App1 Sector" };
        context.Sectors.Add(sector);
        context.SaveChanges();

        var repository = new SectorRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(sector.Id, applicationId: 2));
    }

    [Fact]
    public async Task GetByIdForApplication_SameApplication_ReturnsSector()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sector = new Sector { ApplicationId = 1, Title = "App1 Sector" };
        context.Sectors.Add(sector);
        context.SaveChanges();

        var repository = new SectorRepository(context);

        var result = await repository.GetByIdForApplication(sector.Id, applicationId: 1);

        Assert.Equal(sector.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdForApplication_SoftDeletedSameApplication_Throws()
    {
        // A deleted resource must behave as not found - same outcome as a cross-application id.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sector = new Sector { ApplicationId = 1, Title = "Deleted Sector", IsDeleted = true };
        context.Sectors.Add(sector);
        context.SaveChanges();

        var repository = new SectorRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(sector.Id, applicationId: 1));
    }
}
