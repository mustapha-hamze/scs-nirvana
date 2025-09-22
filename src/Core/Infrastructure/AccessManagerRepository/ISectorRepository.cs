using System.Collections.Generic;
using Domains.Entities.AccessManagement;
using Domains.Entities.General;
using Infrastructure.Repository;

namespace Infrastructure.AccessManagerRepository
{
    public interface ISectorRepository : IRepository<Sector>
    {
        List<Sector> GetAllSector(int applicationId);
        List<Sector> GetAllSector();
    }
}