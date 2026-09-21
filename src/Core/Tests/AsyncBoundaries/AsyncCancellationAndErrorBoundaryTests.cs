using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Xunit;

namespace Core.Tests.AsyncBoundaries;

// Phase 3 ("Modernize Core async and error boundaries"): proves the request-path I/O is
// genuinely cooperative (a caller's CancellationToken is honored, not just accepted and
// ignored) and that failures propagate instead of being swallowed into empty/default data.
public class AsyncCancellationAndErrorBoundaryTests
{
    [Fact]
    public async Task ContentQueryRepository_List_PreCanceledToken_ThrowsInsteadOfRunningTheQuery()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.Contents.Add(new Content { ApplicationId = 1, TypeId = 1000, Title = "Sample" });
        context.SaveChanges();

        var repository = new ContentQueryRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // A pre-canceled token must stop the query before it runs, not after silently ignoring
        // it - regression guard for CancellationToken being accepted but never threaded into
        // the underlying ToListAsync call.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.List(applicationId: 1, cts.Token));
    }

    [Fact]
    public async Task CategoryRepository_GetAllFullPath_PropagatesRealFailures_InsteadOfReturningEmptyList()
    {
        // Regression guard for the exception-swallowing bug this phase removed: a genuine query
        // failure (here, an already-disposed DbContext) must propagate as an exception, not be
        // caught and turned into an empty list indistinguishable from "no categories".
        var factory = new SqliteContextFactory();
        var context = factory.CreateContext();
        var repository = new CategoryRepository(context);
        context.Dispose();
        factory.Dispose();

        await Assert.ThrowsAnyAsync<ObjectDisposedException>(
            () => repository.GetAllFullPath(applicationId: 1));
    }

    [Fact]
    public async Task UnitOfWork_ExecuteInTransactionAsync_OperationThrows_RollsBackAndPropagates()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.UnitOfWork.UnitOfWork>.Instance);

        context.Categories.Add(new Category { ApplicationId = 1, Title = "Staged" });

        // The operation stages a change, then fails - the transaction must roll back (the staged
        // add must not be committed) and the original failure must propagate unwrapped, not be
        // swallowed or replaced by a generic transaction-failure exception.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(() => throw new InvalidOperationException("boom")));

        await using var verifyContext = factory.CreateContext();
        Assert.Empty(verifyContext.Categories);
    }
}
