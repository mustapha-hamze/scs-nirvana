using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Core.Tests.Integration;

// Opt-in SQL Server integration tests. SQLite-backed tests remain the default and cover EF
// query/mutation logic; these exist only to catch the things SQLite can't: real SQL Server
// column/type/precision behavior, and the SP_ContentsInCategory stored procedure path (which
// only exists in a real SQL Server database - see docs/schema-readiness.md).
//
// Enable by setting the CORE_TESTS_SQLSERVER_CONNECTION_STRING environment variable to a
// connection string for a disposable/test SQL Server database before running `dotnet test`.
// With no connection string set (the default, including in CI), every test here returns
// immediately without asserting anything - xunit reports it as passed, not skipped, since the
// project has no skip-with-a-reason mechanism wired up; the intent is the same: this suite does
// not gate a normal build.
public class SqlServerIntegrationTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("CORE_TESTS_SQLSERVER_CONNECTION_STRING");

    private static bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ApplicationDbContext(options, TimeProvider.System);
    }

    [Fact]
    public async Task GetByIdForApplication_TenantScopingAndSoftDeleteFiltering_AgainstRealSqlServer()
    {
        if (!IsConfigured) return;

        await using var context = CreateContext();
        var repository = new ContentQueryRepository(context);

        var owned = new Content { ApplicationId = int.MinValue, TypeId = 1000, Title = "SQL Server integration test row" };
        var deleted = new Content { ApplicationId = int.MinValue, TypeId = 1000, Title = "Deleted", IsDeleted = true };
        context.Contents.AddRange(owned, deleted);
        await context.SaveChangesAsync();

        try
        {
            var result = await repository.GetByIdForApplication(owned.Id, int.MinValue);
            Assert.Equal(owned.Id, result.Id);

            // Same tenant/soft-delete rejection contract as the SQLite-backed tests: a
            // soft-deleted row must be indistinguishable from a missing one, on SQL Server too.
            await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(deleted.Id, int.MinValue));
            await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByIdForApplication(owned.Id, int.MinValue + 1));
        }
        finally
        {
            context.Contents.RemoveRange(owned, deleted);
            await context.SaveChangesAsync();
            // Hard-delete the test rows directly: ApplicationDbContext's lifecycle policy
            // converts a normal Remove() into a soft delete, so undo that here to leave the
            // shared test database exactly as clean as it was found.
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM CMS_Contents WHERE Id = {0} OR Id = {1}", owned.Id, deleted.Id);
        }
    }

    [Fact]
    public async Task GetContentsInCategory_StoredProcedure_ExecutesAgainstRealSqlServer()
    {
        if (!IsConfigured) return;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            })
            .Build();
        var adapter = new ContentsInCategoryQueryAdapter(configuration);

        // A category id that (almost certainly) owns no content is enough to prove the
        // procedure exists, accepts these parameters, and returns rows Dapper can map onto
        // ContentDto - the exact row contents depend on production data this test doesn't
        // control, so it deliberately doesn't assert on them beyond "did not throw".
        var result = await adapter.GetContentsInCategory(categoryId: int.MinValue, applicationId: int.MinValue);

        Assert.NotNull(result);
    }
}
