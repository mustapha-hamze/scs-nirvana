using System.Collections.Generic;
using Domains.Entities.AccessManagement;
using Infrastructure.Repository;

namespace Infrastructure.AccessManagerRepository
{
    public interface ISectorEntityRepository : IRepository<SectorEntity>
    {
        List<SectorEntity> GetSectorEntities(int sectorId);
        List<SectorEntity> GetAllEntities();
    }
}