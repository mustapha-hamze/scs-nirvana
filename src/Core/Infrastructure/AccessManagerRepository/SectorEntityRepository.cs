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
        public Task<List<SectorEntity>> GetSectorEntities(int sectorId, CancellationToken cancellationToken = default)
        {
            return _dbContext.SectorEntities.Where(s => !s.IsDeleted && s.SectorId == sectorId).ToListAsync(cancellationToken);
        }
        public Task<List<SectorEntity>> GetEntitiesForApplication(int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.SectorEntities
                .Where(s => !s.IsDeleted && s.Sector.ApplicationId == applicationId && !s.Sector.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        // SectorEntity has no ApplicationId column; it's resolved through SectorId -> Sector.
        // A soft-deleted entity, or one whose parent Sector is soft-deleted, must not resolve.
        public async Task<SectorEntity> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.SectorEntities.AsNoTracking()
                .SingleAsync(s => s.Id == id && !s.IsDeleted
                    && s.Sector.ApplicationId == applicationId && !s.Sector.IsDeleted, cancellationToken);
        }
    }
}