using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.GeneralRepository;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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
        public Task<List<SystemType>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.SystemTypes
                .Where(s => !s.IsDeleted && s.IsActive && s.ApplicationId == applicationId)
                .OrderByDescending(s => s.CreatedDT).ToListAsync(cancellationToken);
        }

        public Task<List<SystemType>> GetTypesInTypeGroup(int applicationId, int typeGroup, CancellationToken cancellationToken = default)
        {
            return _dbContext.SystemTypes
                .Where(s => !s.IsDeleted && s.IsActive && s.ApplicationId == applicationId && s.TypeGroupId == typeGroup)
                .Order().ToListAsync(cancellationToken);
        }
    }
}