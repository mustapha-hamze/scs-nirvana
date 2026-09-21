using System;
using System.Threading.Tasks;
using Application.UnitOfWork;
using Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<UnitOfWork> _logger;

        public UnitOfWork(ApplicationDbContext dbContext, ILogger<UnitOfWork> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _dbContext.SaveChangesAsync(cancellationToken);

        public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await operation();
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Entity/operation content is never logged here - only that a transaction
                    // failed and why (exception type), never what data was in it. Cancellation is
                    // expected control flow, not a failure, so it's excluded from error logging.
                    if (ex is not OperationCanceledException)
                        _logger.LogError(ex, "Transaction rolled back due to {ExceptionType}", ex.GetType().Name);

                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
    }
}