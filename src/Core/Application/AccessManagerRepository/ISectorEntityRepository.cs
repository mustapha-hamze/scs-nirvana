using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;
using Application.Repository;

namespace Application.AccessManagerRepository
{
    public interface ISectorEntityRepository : IRepository<SectorEntity>
    {
        Task<List<SectorEntity>> GetSectorEntities(int sectorId, CancellationToken cancellationToken = default);
        Task<List<SectorEntity>> GetAllEntities(CancellationToken cancellationToken = default);
        Task<SectorEntity> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}