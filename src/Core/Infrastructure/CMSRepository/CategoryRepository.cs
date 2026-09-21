using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Repository;
using Infrastructure.Data;

namespace Infrastructure.CMSRepository
{
    public class CategoryRepository : Repository<Category>, ICategoryRepository
    {
        private readonly ApplicationDbContext _dbContext;
        public CategoryRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Category> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Categories.AsNoTracking()
                .SingleAsync(c => c.Id == id && c.ApplicationId == applicationId && !c.IsDeleted, cancellationToken);
        }

        public async Task<List<Category>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Categories
                .Where(c => !c.IsDeleted && c.ApplicationId == applicationId && !c.IsDeleted)
                .OrderBy(c => c.Id)
                .ToListAsync(cancellationToken);
        }

        // No try/catch: an empty result here means "no categories for this application", a real
        // outcome the query itself already produces - it must not be confused with a query
        // failure (e.g. a DB outage) by swallowing every exception into the same empty list.
        public async Task<List<Category>> GetAllFullPath(int applicationId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Categories.Where(c => c.ApplicationId == applicationId
                && !c.IsDeleted).ToListAsync(cancellationToken);
        }
    }
}
