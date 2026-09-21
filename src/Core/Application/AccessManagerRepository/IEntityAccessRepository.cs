using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;
using Application.Repository;

namespace Application.AccessManagerRepository
{
    public interface IEntityAccessRepository : IRepository<EntityAccess>
    {
        Task<List<EntityAccess>> List(int applicationId, CancellationToken cancellationToken = default);

        Task<List<EntityAccess>> GetEntityAccesses(int entityId, CancellationToken cancellationToken = default);

        Task<EntityAccess> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}