using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.GeneralRepository;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

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
        public Task<List<Culture>> List(CancellationToken cancellationToken = default)
        {
            return _dbContext.Cultures
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDT).ToListAsync(cancellationToken);
        }
    }
}