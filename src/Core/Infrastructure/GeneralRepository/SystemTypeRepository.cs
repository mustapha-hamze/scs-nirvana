using System.Collections.Generic;
using System.Linq;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.Data.SqlClient;

namespace Infrastructure.GeneralRepository
{
    public class SystemTypeRepository : Repository<SystemType>, ISystemTypeRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;

        // constructor 
        public SystemTypeRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        // methods
        public List<SystemType> List(int applicationId)
        {
            return _dbContext.SystemTypes
                .Where(s => !s.IsDeleted && s.IsActive && s.ApplicationId == applicationId)
                .OrderByDescending(s => s.CreatedDT).ToList();
        }

        public List<SystemType> GetTypesInTypeGroup(int applicationId, int typeGroup)
        {
            return _dbContext.SystemTypes
                .Where(s => !s.IsDeleted && s.IsActive && s.ApplicationId == applicationId && s.TypeGroupId == typeGroup)
                .OrderByDescending(s => s.CreatedDT).ToList();
        }
    }
}