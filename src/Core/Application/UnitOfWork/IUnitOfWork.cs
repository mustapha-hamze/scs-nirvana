using System;
using System.Threading.Tasks;

namespace Application.UnitOfWork
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Runs the operation and a single SaveChanges inside one DB transaction,
        // so multi-step writes succeed or fail together instead of committing per step.
        Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    }
}