using System.Collections.Generic;
using Domains.Entities.AccessManagement;
using Application.Repository;

namespace Application.AccessManagerRepository
{
    public interface ISectorEntityRepository : IRepository<SectorEntity>
    {
        List<SectorEntity> GetSectorEntities(int sectorId);
        List<SectorEntity> GetAllEntities();
    }
}