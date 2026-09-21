using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Core.Tests.TestSupport;

// Creates an ApplicationDbContext backed by an open, in-memory SQLite connection.
// SQLite (unlike EF Core's InMemory provider) supports real relational transactions,
// which is required to exercise IUnitOfWork.ExecuteInTransactionAsync meaningfully.
public sealed class SqliteContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // These tests exercise repository/unit-of-work logic, not referential integrity
        // (the real schema is managed outside EF migrations), so FK enforcement is disabled.
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    // timeProvider defaults to the real clock; pass a fake (e.g. Core.Tests.TestSupport.FakeTimeProvider)
    // to assert on the exact CreatedDT/UpdatedDT ApplicationDbContext stamps at SaveChanges time.
    public ApplicationDbContext CreateContext(TimeProvider timeProvider = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ApplicationDbContext(options, timeProvider ?? TimeProvider.System);
    }

    public void Dispose() => _connection.Dispose();
}
