using Core.Tests.TestSupport;
using Domains.Entities.General;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.Data;

// ApplicationDbContext.ApplyLifecyclePolicy is the one place BaseEntity's CreatedDT/UpdatedDT and
// soft-delete conversion happen - these tests exercise that mechanism directly (via TimeProvider
// and raw ChangeTracker operations) rather than through any one repository, to prove it's a
// context-level policy and not something tied to Repository<T>'s specific code path.
public class EntityLifecyclePolicyTests
{
    [Fact]
    public async Task Create_StampsCreatedAndUpdatedDT_FromInjectedTimeProvider_InUtc()
    {
        using var factory = new SqliteContextFactory();
        var fakeNow = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);
        using var context = factory.CreateContext(new FakeTimeProvider(fakeNow));

        var repository = new Repository<Culture>(context);
        var created = await repository.Create(new Culture { ApplicationId = 1, Title = "English", Key = "en" });
        await context.SaveChangesAsync();

        Assert.Equal(fakeNow.UtcDateTime, created.CreatedDT);
        Assert.Equal(fakeNow.UtcDateTime, created.UpdatedDT);
    }

    [Fact]
    public async Task Update_BumpsUpdatedDT_ToCurrentFakeTime_ButPreservesCreatedDT()
    {
        using var factory = new SqliteContextFactory();
        var createdAt = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(createdAt);

        int cultureId;
        using (var context = factory.CreateContext(timeProvider))
        {
            var repository = new Repository<Culture>(context);
            var created = await repository.Create(new Culture { ApplicationId = 1, Title = "English", Key = "en" });
            await context.SaveChangesAsync();
            cultureId = created.Id;
        }

        var updatedAt = createdAt.AddDays(1);
        timeProvider.SetUtcNow(updatedAt);
        using (var context = factory.CreateContext(timeProvider))
        {
            var repository = new Repository<Culture>(context);
            var existing = await repository.GetById(cultureId);
            existing.Title = "English (updated)";
            await repository.Update(existing);
            await context.SaveChangesAsync();
        }

        await using var verifyContext = factory.CreateContext();
        var stored = await verifyContext.Set<Culture>().SingleAsync(c => c.Id == cultureId);
        Assert.Equal(createdAt.UtcDateTime, stored.CreatedDT);
        Assert.Equal(updatedAt.UtcDateTime, stored.UpdatedDT);
    }

    [Fact]
    public void RemovingAnEntity_IsConvertedToASoftDelete_RegardlessOfWhichCodePathRemovedIt()
    {
        // Uses DbSet.Remove directly (bypassing Repository<T> entirely) to prove the soft-delete
        // conversion is a context-level policy, not logic duplicated inside Repository<T>.Delete.
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var culture = new Culture { ApplicationId = 1, Title = "Farsi", Key = "fa" };
        context.Set<Culture>().Add(culture);
        context.SaveChanges();

        context.Set<Culture>().Remove(culture);
        context.SaveChanges();

        // Administrative/audit case: proving the row still physically exists (soft delete, not a
        // hard delete) requires seeing past the global "exclude soft-deleted rows" filter.
        var stored = context.Set<Culture>().IgnoreQueryFilters().Single(c => c.Id == culture.Id);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public void Query_ExcludesSoftDeletedRows_ByDefault()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.Set<Culture>().Add(new Culture { ApplicationId = 1, Title = "Active", Key = "en" });
        context.Set<Culture>().Add(new Culture { ApplicationId = 1, Title = "Deleted", Key = "fa", IsDeleted = true });
        context.SaveChanges();

        var visible = context.Set<Culture>().ToList();

        var culture = Assert.Single(visible);
        Assert.Equal("Active", culture.Title);
    }

    [Fact]
    public void IgnoreQueryFilters_ExplicitlyOptsOutOfTheGlobalFilter_ForAdministrativeAudit()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.Set<Culture>().Add(new Culture { ApplicationId = 1, Title = "Deleted", Key = "fa", IsDeleted = true });
        context.SaveChanges();

        Assert.Empty(context.Set<Culture>().ToList());
        Assert.Single(context.Set<Culture>().IgnoreQueryFilters().ToList());
    }
}
