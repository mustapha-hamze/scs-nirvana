using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;
using Infrastructure.Dto.AccessManagerDtos;

namespace Services.AccessManagerServices
{
    public interface ISectorServices
    {
        Task Create(SectorDto sector);
        Task Update(SectorDto sector);
        Task<SectorDto> GetById(int id);
        Task Delete(int id);
        List<SectorDto> GetAllSector(int applicationId);
        List<SectorDto> GetAllSector();
    }
}