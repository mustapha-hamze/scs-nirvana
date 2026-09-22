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

    [Fact]
    public async Task GetEntitiesForApplication_SameApplication_ReturnsEntity()
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

        var result = await repository.GetEntitiesForApplication(applicationId: 1);

        var entity = Assert.Single(result);
        Assert.Equal(sectorEntity.Id, entity.Id);
    }

    [Fact]
    public async Task GetEntitiesForApplication_CrossApplicationEntity_IsExcluded()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sectorApp1 = new Sector { ApplicationId = 1, Title = "Sector 1" };
        var sectorApp2 = new Sector { ApplicationId = 2, Title = "Sector 2" };
        context.Sectors.AddRange(sectorApp1, sectorApp2);
        context.SaveChanges();
        context.SectorEntities.AddRange(
            new SectorEntity { SectorId = sectorApp1.Id, Title = "Entity 1", AccessKey = "key1" },
            new SectorEntity { SectorId = sectorApp2.Id, Title = "Entity 2", AccessKey = "key2" });
        context.SaveChanges();

        var repository = new SectorEntityRepository(context);

        var result = await repository.GetEntitiesForApplication(applicationId: 1);

        var entity = Assert.Single(result);
        Assert.Equal("Entity 1", entity.Title);
    }

    [Fact]
    public async Task GetEntitiesForApplication_SoftDeletedEntity_IsExcluded()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sector = new Sector { ApplicationId = 1, Title = "Sector" };
        context.Sectors.Add(sector);
        context.SaveChanges();
        var sectorEntity = new SectorEntity { SectorId = sector.Id, Title = "Entity", AccessKey = "key", IsDeleted = true };
        context.SectorEntities.Add(sectorEntity);
        context.SaveChanges();

        var repository = new SectorEntityRepository(context);

        var result = await repository.GetEntitiesForApplication(applicationId: 1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEntitiesForApplication_SoftDeletedParentSector_IsExcluded()
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

        var result = await repository.GetEntitiesForApplication(applicationId: 1);

        Assert.Empty(result);
    }
}
