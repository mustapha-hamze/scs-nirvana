using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.AccessManagement;

namespace Application.UseCases.AccessManagerServices
{
    public interface ISectorEntityServices
    {
        Task Create(SectorEntityDto sectorEntity, int applicationId, CancellationToken cancellationToken = default);
        Task Update(SectorEntityDto sectorEntity, int applicationId, CancellationToken cancellationToken = default);

        // Application-scoped: validates sectorId belongs to applicationId first. Used by the
        // per-application BackOffice sector management screens.
        Task<List<SectorEntityDto>> GetSectorEntities(int sectorId, int applicationId, CancellationToken cancellationToken = default);

        // Unscoped: used by the cross-application user/access-administration screens (e.g.
        // AccountController), where an admin is deliberately working across applications and
        // already chose the sector from an application they picked explicitly.
        Task<List<SectorEntityDto>> GetSectorEntities(int sectorId, CancellationToken cancellationToken = default);
        Task<List<SectorEntityDto>> GetAllEntities(CancellationToken cancellationToken = default);
        Task<SectorEntityDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}