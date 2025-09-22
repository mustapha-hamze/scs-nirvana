using System.Collections.Generic;
using System.Linq;
using Domains.Entities.AccessManagement;
using Infrastructure.Data;
using Infrastructure.Repository;

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
        public List<EntityAccess> List(int applicationId)
        {
            return _dbContext.EntityAccesses.Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.CreatedDT).ToList();
        }

        public List<EntityAccess> GetEntityAccesses(int entityId)
        {
            return _dbContext.EntityAccesses.Where(a => a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedDT).ToList();
        }
    }
}