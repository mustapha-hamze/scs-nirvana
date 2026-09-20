using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;
using Domains.Entities.General;
using Application.Repository;

namespace Application.AccessManagerRepository
{
    public interface ISectorRepository : IRepository<Sector>
    {
        List<Sector> GetAllSector(int applicationId);
        List<Sector> GetAllSector();
        Task<Sector> GetByIdForApplication(int id, int applicationId);
    }
}