using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.AccessManagement;

namespace Services.AccessManagerServices
{
    public interface ISectorEntityServices
    {
        Task Create(SectorEntityDto sectorEntity, int applicationId);
        Task Update(SectorEntityDto sectorEntity, int applicationId);

        // Application-scoped: validates sectorId belongs to applicationId first. Used by the
        // per-application BackOffice sector management screens.
        List<SectorEntityDto> GetSectorEntities(int sectorId, int applicationId);

        // Unscoped: used by the cross-application user/access-administration screens (e.g.
        // AccountController), where an admin is deliberately working across applications and
        // already chose the sector from an application they picked explicitly.
        List<SectorEntityDto> GetSectorEntities(int sectorId);
        List<SectorEntityDto> GetAllEntities();
        Task<SectorEntityDto> GetById(int id, int applicationId);
    }
}