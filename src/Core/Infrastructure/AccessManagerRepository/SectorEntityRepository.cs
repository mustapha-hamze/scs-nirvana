using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.AccessManagerRepository;
using Domains.Entities.AccessManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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

        // SectorEntity has no ApplicationId column; it's resolved through SectorId -> Sector.
        public async Task<SectorEntity> GetByIdForApplication(int id, int applicationId)
        {
            return await _dbContext.SectorEntities.AsNoTracking()
                .SingleAsync(s => s.Id == id && s.Sector.ApplicationId == applicationId);
        }
    }
}