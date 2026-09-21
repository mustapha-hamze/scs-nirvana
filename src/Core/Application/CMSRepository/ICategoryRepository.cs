using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;
using Application.Repository;

namespace Application.CMSRepository
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<List<Category>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<List<Category>> GetAllFullPath(int applicationId, CancellationToken cancellationToken = default);

        // Throws (SingleAsync) unless the category exists, is not soft-deleted, and belongs to
        // applicationId - missing, cross-application, and soft-deleted are indistinguishable.
        Task<Category> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}
