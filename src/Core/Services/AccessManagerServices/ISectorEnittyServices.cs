using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.AccessManagement;

namespace Services.AccessManagerServices
{
    public interface ISectorEntityServices
    {
        Task Create(SectorEntityDto sectorEntity);
        Task Update(SectorEntityDto sectorEntity);
        List<SectorEntityDto> GetSectorEntities(int sectorId);
        List<SectorEntityDto> GetAllEntities();
        Task<SectorEntityDto> GetById(int id);
    }
}