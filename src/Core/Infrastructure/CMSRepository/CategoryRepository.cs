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

        public async Task<Category> GetByIdForApplication(int id, int applicationId)
        {
            return await _dbContext.Categories.AsNoTracking()
                .SingleAsync(c => c.Id == id && c.ApplicationId == applicationId && !c.IsDeleted);
        }

        public List<Category> List(int applicationId)
        {
            var categories = _dbContext.Categories
                .Where(c => !c.IsDeleted && c.ApplicationId == applicationId && !c.IsDeleted)
                .OrderBy(c => c.Id)
                .ToList();

            return categories;
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
