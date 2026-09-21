using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;
using Domains.Entities.General;
using Application.Repository;

namespace Application.AccessManagerRepository
{
    public interface ISectorRepository : IRepository<Sector>
    {
        Task<List<Sector>> GetAllSector(int applicationId, CancellationToken cancellationToken = default);
        Task<List<Sector>> GetAllSector(CancellationToken cancellationToken = default);
        Task<Sector> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}