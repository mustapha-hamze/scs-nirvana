using Domains.Entities.ContentManagement;

namespace Application.ContentManagement;

public interface ICategoryProvider
{
    Task<Category> GetById(int categoryId);
}