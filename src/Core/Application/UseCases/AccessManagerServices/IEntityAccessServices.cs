using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.AccessManagement;

namespace Application.UseCases.AccessManagerServices
{
    public interface IEntityAccessServices
    {
        Task Create(EntityAccessDto access, int applicationId, CancellationToken cancellationToken = default);
        Task Update(EntityAccessDto access, int applicationId, CancellationToken cancellationToken = default);
        Task<List<EntityAccessDto>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<EntityAccessDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<EntityAccessDto>> GetEntityAccesses(int entityId, CancellationToken cancellationToken = default);
    }
}