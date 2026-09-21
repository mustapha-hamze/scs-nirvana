using System.Linq;
using Core.Tests.TestSupport;
using Domains.Entities.General;
using Infrastructure.GeneralRepository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.GeneralRepository;

public class ApplicationRepositoryTests
{
    [Fact]
    public async Task ExistsActiveApplication_ActiveNonDeleted_ReturnsTrue()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var app = new Domains.Entities.General.Application { Title = "App", IsActive = true };
        context.Applications.Add(app);
        context.SaveChanges();

        var repository = new ApplicationRepository(context);

        Assert.True(await repository.ExistsActiveApplication(app.Id));
    }

    [Fact]
    public async Task ExistsActiveApplication_Missing_ReturnsFalse()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var repository = new ApplicationRepository(context);

        Assert.False(await repository.ExistsActiveApplication(999));
    }

    [Fact]
    public async Task ExistsActiveApplication_SoftDeleted_ReturnsFalse()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var app = new Domains.Entities.General.Application { Title = "App", IsActive = true, IsDeleted = true };
        context.Applications.Add(app);
        context.SaveChanges();

        var repository = new ApplicationRepository(context);

        Assert.False(await repository.ExistsActiveApplication(app.Id));
    }

    [Fact]
    public async Task ExistsActiveApplication_Inactive_ReturnsFalse()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var app = new Domains.Entities.General.Application { Title = "App", IsActive = false };
        context.Applications.Add(app);
        context.SaveChanges();

        var repository = new ApplicationRepository(context);

        Assert.False(await repository.ExistsActiveApplication(app.Id));
    }

    [Fact]
    public async Task AddUserToApplication_NewPair_InsertsOneRow()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var repository = new ApplicationRepository(context);

        await repository.AddUserToApplication("u1", 5);
        await context.SaveChangesAsync();

        var rows = context.UserInApplications.Where(m => m.UserId == "u1" && m.ApplicationId == 5).ToList();
        var row = Assert.Single(rows);
        Assert.True(row.IsActive);
        Assert.False(row.IsDeleted);
    }

    [Fact]
    public async Task AddUserToApplication_ExistingSoftDeletedRow_RestoresInsteadOfDuplicating()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var existing = new UserInApplication { UserId = "u1", ApplicationId = 5, IsActive = false, IsDeleted = true };
        context.UserInApplications.Add(existing);
        await context.SaveChangesAsync();

        var repository = new ApplicationRepository(context);

        await repository.AddUserToApplication("u1", 5);
        await context.SaveChangesAsync();

        var rows = context.UserInApplications.Where(m => m.UserId == "u1" && m.ApplicationId == 5).ToList();
        var row = Assert.Single(rows);
        Assert.Equal(existing.Id, row.Id);
        Assert.True(row.IsActive);
        Assert.False(row.IsDeleted);
    }

    [Fact]
    public async Task AddUserToApplication_ExistingActiveRow_RemainsIdempotent()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var existing = new UserInApplication { UserId = "u1", ApplicationId = 5, IsActive = true };
        context.UserInApplications.Add(existing);
        await context.SaveChangesAsync();

        var repository = new ApplicationRepository(context);

        await repository.AddUserToApplication("u1", 5);
        await context.SaveChangesAsync();

        var rows = context.UserInApplications.Where(m => m.UserId == "u1" && m.ApplicationId == 5).ToList();
        Assert.Single(rows);
    }

    [Fact]
    public async Task UserInApplication_DuplicateActivePair_ViolatesUniqueConstraint()
    {
        // Defense in depth at the model level: even a caller that bypasses
        // AddUserToApplication's own restore-instead-of-insert logic can't land two active rows
        // for the same (UserId, ApplicationId) pair.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.UserInApplications.Add(new UserInApplication { UserId = "u1", ApplicationId = 5 });
        await context.SaveChangesAsync();

        context.UserInApplications.Add(new UserInApplication { UserId = "u1", ApplicationId = 5 });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task UserAccess_DuplicatePair_ViolatesUniqueConstraint()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.UserAccesses.Add(new UserAccess { UserId = "u1", ApplicationId = 5, Access = "read" });
        await context.SaveChangesAsync();

        context.UserAccesses.Add(new UserAccess { UserId = "u1", ApplicationId = 5, Access = "write" });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
