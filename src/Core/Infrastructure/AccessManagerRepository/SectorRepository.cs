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
            return _dbContext.Sectors.Where(s => !s.IsDeleted).OrderByDescending(s => s.CreatedDT).ToList();
        }
        public List<Sector> GetAllSector()
        {
            return _dbContext.Sectors.Where(s => !s.IsDeleted).OrderByDescending(s => s.CreatedDT).ToList();
        }
    }
}