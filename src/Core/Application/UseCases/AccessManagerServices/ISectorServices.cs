using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;
using Application.Contracts.AccessManagement;

namespace Application.UseCases.AccessManagerServices
{
    public interface ISectorServices
    {
        Task Create(SectorDto sector);
        Task Update(SectorDto sector, int applicationId);
        Task<SectorDto> GetById(int id, int applicationId);
        Task Delete(int id, int applicationId);
        List<SectorDto> GetAllSector(int applicationId);
        List<SectorDto> GetAllSector();
    }
}