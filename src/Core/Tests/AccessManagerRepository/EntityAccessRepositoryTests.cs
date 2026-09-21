using Core.Tests.TestSupport;
using Domains.Entities.AccessManagement;
using Infrastructure.AccessManagerRepository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.AccessManagerRepository;

public class EntityAccessRepositoryTests
{
    // EntityAccess has no ApplicationId column of its own; ownership is resolved through
    // EntityId -> SectorEntity -> SectorId -> Sector -> ApplicationId.
    private static (Sector sector, SectorEntity sectorEntity) SeedSectorTree(
        Infrastructure.Data.ApplicationDbContext context, int applicationId)
    {
        var sector = new Sector { ApplicationId = applicationId, Title = $"Sector-{applicationId}" };
        context.Sectors.Add(sector);
        context.SaveChanges();

        var sectorEntity = new SectorEntity { SectorId = sector.Id, Title = "Entity", AccessKey = "key" };
        context.SectorEntities.Add(sectorEntity);
        context.SaveChanges();

        return (sector, sectorEntity);
    }

    [Fact]
    public async Task List_IgnoredApplicationId_OnlyReturnsThatApplicationsAccesses()
    {
        // Regression guard: List(applicationId) used to ignore its parameter entirely and
        // return every application's EntityAccess rows.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var (_, entityApp1) = SeedSectorTree(context, applicationId: 1);
        var (_, entityApp2) = SeedSectorTree(context, applicationId: 2);

        context.EntityAccesses.Add(new EntityAccess { EntityId = entityApp1.Id, Access = "read" });
        context.EntityAccesses.Add(new EntityAccess { EntityId = entityApp2.Id, Access = "read" });
        context.SaveChanges();

        var repository = new EntityAccessRepository(context);

        var result = await repository.List(applicationId: 1);

        Assert.Single(result);
        Assert.Equal(entityApp1.Id, result[0].EntityId);
    }

    [Fact]
    public async Task List_ExcludesSoftDeletedRows()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var (_, entity) = SeedSectorTree(context, applicationId: 1);

        context.EntityAccesses.Add(new EntityAccess { EntityId = entity.Id, Access = "active", IsDeleted = false });
        context.EntityAccesses.Add(new EntityAccess { EntityId = entity.Id, Access = "deleted", IsDeleted = true });
        context.SaveChanges();

        var repository = new EntityAccessRepository(context);

        var result = await repository.List(applicationId: 1);

        Assert.Single(result);
        Assert.Equal("active", result[0].Access);
    }

    [Fact]
    public async Task GetByIdForApplication_CrossApplicationId_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var (_, entityApp1) = SeedSectorTree(context, applicationId: 1);
        var access = new EntityAccess { EntityId = entityApp1.Id, Access = "read" };
        context.EntityAccesses.Add(access);
        context.SaveChanges();

        var repository = new EntityAccessRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(access.Id, applicationId: 2));
    }

    [Fact]
    public async Task GetByIdForApplication_SameApplication_ReturnsAccess()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var (_, entityApp1) = SeedSectorTree(context, applicationId: 1);
        var access = new EntityAccess { EntityId = entityApp1.Id, Access = "read" };
        context.EntityAccesses.Add(access);
        context.SaveChanges();

        var repository = new EntityAccessRepository(context);

        var result = await repository.GetByIdForApplication(access.Id, applicationId: 1);

        Assert.Equal(access.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdForApplication_SoftDeletedSameApplication_Throws()
    {
        // A deleted resource must behave as not found - same outcome as a cross-application id.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var (_, entity) = SeedSectorTree(context, applicationId: 1);
        var access = new EntityAccess { EntityId = entity.Id, Access = "read", IsDeleted = true };
        context.EntityAccesses.Add(access);
        context.SaveChanges();

        var repository = new EntityAccessRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(access.Id, applicationId: 1));
    }

    [Fact]
    public async Task GetByIdForApplication_ParentSectorEntitySoftDeleted_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var (_, entity) = SeedSectorTree(context, applicationId: 1);
        entity.IsDeleted = true;
        var access = new EntityAccess { EntityId = entity.Id, Access = "read" };
        context.EntityAccesses.Add(access);
        context.SaveChanges();

        var repository = new EntityAccessRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(access.Id, applicationId: 1));
    }

    [Fact]
    public async Task GetByIdForApplication_GrandparentSectorSoftDeleted_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var (sector, entity) = SeedSectorTree(context, applicationId: 1);
        sector.IsDeleted = true;
        var access = new EntityAccess { EntityId = entity.Id, Access = "read" };
        context.EntityAccesses.Add(access);
        context.SaveChanges();

        var repository = new EntityAccessRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(access.Id, applicationId: 1));
    }

    [Fact]
    public async Task GetEntityAccesses_ExcludesSoftDeletedRows()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var (_, entity) = SeedSectorTree(context, applicationId: 1);
        context.EntityAccesses.Add(new EntityAccess { EntityId = entity.Id, Access = "active", IsDeleted = false });
        context.EntityAccesses.Add(new EntityAccess { EntityId = entity.Id, Access = "deleted", IsDeleted = true });
        context.SaveChanges();

        var repository = new EntityAccessRepository(context);

        var result = await repository.GetEntityAccesses(entity.Id);

        var access = Assert.Single(result);
        Assert.Equal("active", access.Access);
    }
}
