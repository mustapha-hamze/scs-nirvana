using System.Collections.Generic;
using System.Linq;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;

namespace Infrastructure.GeneralRepository
{
    public class CultureRepository : Repository<Culture>, ICultureRepository
    {
        // fields
        private readonly ApplicationDbContext _dbContext;


        // constructor
        public CultureRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        // methods
        public List<Culture> List()
        {
            return _dbContext.Cultures
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDT).ToList();
        }
    }
}