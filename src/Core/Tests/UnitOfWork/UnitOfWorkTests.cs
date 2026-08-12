using Core.Tests.TestSupport;
using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.UnitOfWork;

public class UnitOfWorkTests
{
    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsOnSuccess()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();
        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context);

        await unitOfWork.ExecuteInTransactionAsync(() =>
        {
            context.Set<Culture>().Add(new Culture { ApplicationId = 1, Title = "English", Key = "en" });
            context.Set<Culture>().Add(new Culture { ApplicationId = 1, Title = "Farsi", Key = "fa" });
            return Task.CompletedTask;
        });

        await using var verifyContext = factory.CreateContext();
        Assert.Equal(2, await verifyContext.Set<Culture>().CountAsync());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackAllWritesOnFailure()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();
        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(() =>
            {
                context.Set<Culture>().Add(new Culture { ApplicationId = 1, Title = "English", Key = "en" });
                throw new InvalidOperationException("simulated failure mid-operation");
            }));

        await using var verifyContext = factory.CreateContext();
        Assert.Equal(0, await verifyContext.Set<Culture>().CountAsync());
    }
}
