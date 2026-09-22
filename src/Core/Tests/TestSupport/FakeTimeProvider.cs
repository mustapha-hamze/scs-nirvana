namespace Core.Tests.TestSupport;

// A minimal fake clock for asserting the exact UTC timestamp ApplicationDbContext stamps onto
// CreatedDT/UpdatedDT at SaveChanges time.
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
}
