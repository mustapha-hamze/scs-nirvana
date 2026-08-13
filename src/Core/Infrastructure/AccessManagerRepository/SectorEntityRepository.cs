using System.Collections.Generic;
using System.Linq;
using Application.AccessManagerRepository;
using Domains.Entities.AccessManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.Data.SqlClient;

namespace Infrastructure.AccessManagerRepository
{
    public class SectorEntityRepository : Repository<SectorEntity>, ISectorEntityRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;

        // constructor
        public SectorEntityRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        // methods
        public List<SectorEntity> GetSectorEntities(int sectorId)
        {
            return _dbContext.SectorEntities.Where(s => !s.IsDeleted && s.SectorId == sectorId).ToList();
        }
        public List<SectorEntity> GetAllEntities()
        {
            return _dbContext.SectorEntities.Where(s => !s.IsDeleted).ToList();
        }
    }
}