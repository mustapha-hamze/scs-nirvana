using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;

namespace Application.AccessManagerRepository
{
    public interface ISectorRepository
    {
        Task<Sector> Create(Sector sector);
        Task<Sector> Update(Sector sector);

        // Requires applicationId so a sector id can't be deleted from any application but its own.
        Task Delete(int id, int applicationId, CancellationToken cancellationToken = default);

        Task<List<Sector>> GetAllSector(int applicationId, CancellationToken cancellationToken = default);
        Task<List<Sector>> GetAllSector(CancellationToken cancellationToken = default);
        Task<Sector> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}