using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;
using Application.Contracts.AccessManagement;

namespace Application.UseCases.AccessManagerServices
{
    public interface ISectorServices
    {
        Task Create(SectorDto sector, CancellationToken cancellationToken = default);
        Task Update(SectorDto sector, int applicationId, CancellationToken cancellationToken = default);
        Task<SectorDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default);
        Task Delete(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<SectorDto>> GetAllSector(int applicationId, CancellationToken cancellationToken = default);
        Task<List<SectorDto>> GetAllSector(CancellationToken cancellationToken = default);
    }
}