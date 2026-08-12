using Application.ContentManagement;
using Domains.Entities.ContentManagement;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.ContentManagement;

public class CategoryProvider : ICategoryProvider
{
    private readonly ApplicationDbContext _dbContext;
    public CategoryProvider(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Category> GetById(int categoryId)
    {
        return await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.IsActive && !c.IsDeleted);
    }
}