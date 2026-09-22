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

        // SectorEntity has no ApplicationId column; resolved through SectorId -> Sector ->
        // ApplicationId. Excludes a soft-deleted entity and one whose parent Sector is
        // soft-deleted or belongs to a different application.
        Task<List<SectorEntity>> GetEntitiesForApplication(int applicationId, CancellationToken cancellationToken = default);
        Task<SectorEntity> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}