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
        public List<Sector> GetAllSector(int applicationId)
        {
            return _dbContext.Sectors.Where(s => !s.IsDeleted && s.ApplicationId == applicationId)
                .OrderByDescending(s => s.CreatedDT).ToList();
        }
        public List<Sector> GetAllSector()
        {
            return _dbContext.Sectors.Where(s => !s.IsDeleted).OrderByDescending(s => s.CreatedDT).ToList();
        }

        public async Task<Sector> GetByIdForApplication(int id, int applicationId)
        {
            return await _dbContext.Sectors.AsNoTracking()
                .SingleAsync(s => s.Id == id && s.ApplicationId == applicationId && !s.IsDeleted);
        }
    }
}