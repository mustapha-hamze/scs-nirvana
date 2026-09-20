using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.AccessManagement;
using Application.Repository;

namespace Application.AccessManagerRepository
{
    public interface ISectorEntityRepository : IRepository<SectorEntity>
    {
        List<SectorEntity> GetSectorEntities(int sectorId);
        List<SectorEntity> GetAllEntities();
        Task<SectorEntity> GetByIdForApplication(int id, int applicationId);
    }
}