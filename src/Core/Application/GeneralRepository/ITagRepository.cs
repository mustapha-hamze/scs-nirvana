using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;
using Application.Repository;

namespace Application.GeneralRepository
{
    public interface ITagRepository : IRepository<Domains.Entities.General.Tag>
    {
        Task<List<Domains.Entities.General.Tag>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<List<Domains.Entities.General.Tag>> FindTagsByTypeId(int applicationId, int typeId, CancellationToken cancellationToken = default);

        // Throws (SingleAsync) unless the tag exists, is not soft-deleted, and belongs to
        // applicationId - missing, cross-application, and soft-deleted are indistinguishable.
        Task<Domains.Entities.General.Tag> GetByIdForApplication(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}