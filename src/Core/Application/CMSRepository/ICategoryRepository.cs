using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;

namespace Application.CMSRepository
{
    public interface ICategoryRepository
    {
        Task<Category> Create(Category category);

        // Requires applicationId so a category id can't be deleted from any application but its own.
        Task Delete(int id, int applicationId, CancellationToken cancellationToken = default);

        Task<List<Category>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<List<Category>> GetAllFullPath(int applicationId, CancellationToken cancellationToken = default);

        // Throws (SingleAsync) unless the category exists, is not soft-deleted, and belongs to
        // applicationId - missing, cross-application, and soft-deleted are indistinguishable.
        Task<Category> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}
