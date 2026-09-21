using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.AccessManagerRepository;
using Domains.Entities.AccessManagement;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.AccessManagerRepository
{
    public class EntityAccessRepository : Repository<EntityAccess>, IEntityAccessRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;

        // constructor
        public EntityAccessRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        // methods
        public Task<List<EntityAccess>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.EntityAccesses
            .Where(a => !a.IsDeleted && !a.SectorEntity.IsDeleted
                && a.SectorEntity.Sector.ApplicationId == applicationId && !a.SectorEntity.Sector.IsDeleted)
            .OrderByDescending(a => a.CreatedDT).ToListAsync(cancellationToken);
        }

        public Task<List<EntityAccess>> GetEntityAccesses(int entityId, CancellationToken cancellationToken = default)
        {
            return _dbContext.EntityAccesses.Where(a => !a.IsDeleted && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedDT).ToListAsync(cancellationToken);
        }

        // Resolves ApplicationId through EntityId -> SectorEntity -> Sector, since EntityAccess
        // has no ApplicationId column of its own. Throws (matching IRepository<T>.GetById's
        // existing SingleAsync-throws behavior) instead of returning null, so a wrong-application
        // id looks identical to a missing one to the caller. A soft-deleted access, or one whose
        // parent SectorEntity/Sector is soft-deleted, must not resolve either.
        public async Task<EntityAccess> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.EntityAccesses.AsNoTracking()
                .SingleAsync(a => a.Id == id && !a.IsDeleted
                    && !a.SectorEntity.IsDeleted
                    && a.SectorEntity.Sector.ApplicationId == applicationId && !a.SectorEntity.Sector.IsDeleted, cancellationToken);
        }
    }
}