using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;
using Dapper;
using Microsoft.Data.SqlClient;
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

        public List<Category> List(int applicationId)
        {
            return _dbContext.Categories
                .Where(c => !c.IsDeleted && c.ApplicationId == applicationId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDT)
                .ToList();
        }

        public List<Category> GetAllFullPath(int applicationId)
        {
            try
            {
                return _dbContext.Categories.Where(c => c.ApplicationId == applicationId
                    && !c.IsDeleted).ToList();
            }
            catch (Exception)
            {
                return new List<Category>();
            }
        }
    }
}
