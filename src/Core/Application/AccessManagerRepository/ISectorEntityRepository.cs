using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;

namespace Application.AccessManagerRepository
{
    public interface ISectorEntityRepository
    {
        Task<SectorEntity> Create(SectorEntity sectorEntity);
        Task<SectorEntity> Update(SectorEntity sectorEntity);

        Task<List<SectorEntity>> GetSectorEntities(int sectorId, CancellationToken cancellationToken = default);
        Task<List<SectorEntity>> GetAllEntities(CancellationToken cancellationToken = default);
        Task<SectorEntity> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}