using Core.Tests.TestSupport;
using Domains.Entities.AccessManagement;
using Infrastructure.AccessManagerRepository;
using Xunit;

namespace Core.Tests.AccessManagerRepository;

public class SectorEntityRepositoryTests
{
    [Fact]
    public async Task GetByIdForApplication_SameApplication_ReturnsSectorEntity()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sector = new Sector { ApplicationId = 1, Title = "Sector" };
        context.Sectors.Add(sector);
        context.SaveChanges();
        var sectorEntity = new SectorEntity { SectorId = sector.Id, Title = "Entity", AccessKey = "key" };
        context.SectorEntities.Add(sectorEntity);
        context.SaveChanges();

        var repository = new SectorEntityRepository(context);

        var result = await repository.GetByIdForApplication(sectorEntity.Id, applicationId: 1);

        Assert.Equal(sectorEntity.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdForApplication_CrossApplicationId_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sector = new Sector { ApplicationId = 1, Title = "Sector" };
        context.Sectors.Add(sector);
        context.SaveChanges();
        var sectorEntity = new SectorEntity { SectorId = sector.Id, Title = "Entity", AccessKey = "key" };
        context.SectorEntities.Add(sectorEntity);
        context.SaveChanges();

        var repository = new SectorEntityRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(sectorEntity.Id, applicationId: 2));
    }

    [Fact]
    public async Task GetByIdForApplication_SoftDeletedSameApplication_Throws()
    {
        // A deleted resource must behave as not found - same outcome as a cross-application id.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sector = new Sector { ApplicationId = 1, Title = "Sector" };
        context.Sectors.Add(sector);
        context.SaveChanges();
        var sectorEntity = new SectorEntity { SectorId = sector.Id, Title = "Entity", AccessKey = "key", IsDeleted = true };
        context.SectorEntities.Add(sectorEntity);
        context.SaveChanges();

        var repository = new SectorEntityRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(sectorEntity.Id, applicationId: 1));
    }

    [Fact]
    public async Task GetByIdForApplication_ParentSectorSoftDeleted_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sector = new Sector { ApplicationId = 1, Title = "Sector", IsDeleted = true };
        context.Sectors.Add(sector);
        context.SaveChanges();
        var sectorEntity = new SectorEntity { SectorId = sector.Id, Title = "Entity", AccessKey = "key" };
        context.SectorEntities.Add(sectorEntity);
        context.SaveChanges();

        var repository = new SectorEntityRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(sectorEntity.Id, applicationId: 1));
    }
}
