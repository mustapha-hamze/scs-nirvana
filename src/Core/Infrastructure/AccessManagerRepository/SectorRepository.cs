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
    public class SectorRepository : Repository<Sector>, ISectorRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;

        // constructor
        public SectorRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        // methods
        public Task<List<Sector>> GetAllSector(int applicationId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Sectors.Where(s => !s.IsDeleted && s.ApplicationId == applicationId)
                .OrderByDescending(s => s.CreatedDT).ToListAsync(cancellationToken);
        }
        public Task<List<Sector>> GetAllSector(CancellationToken cancellationToken = default)
        {
            return _dbContext.Sectors.Where(s => !s.IsDeleted).OrderByDescending(s => s.CreatedDT).ToListAsync(cancellationToken);
        }

        public async Task<Sector> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Sectors.AsNoTracking()
                .SingleAsync(s => s.Id == id && s.ApplicationId == applicationId && !s.IsDeleted, cancellationToken);
        }

        public async Task Delete(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            var sector = await _dbContext.Sectors
                .SingleAsync(s => s.Id == id && s.ApplicationId == applicationId && !s.IsDeleted, cancellationToken);
            _dbContext.Sectors.Remove(sector);
        }
    }
}