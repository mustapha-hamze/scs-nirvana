using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;
using Application.Repository;

namespace Application.GeneralRepository
{
    public interface ITagRepository : IRepository<Domains.Entities.General.Tag>
    {
        List<Domains.Entities.General.Tag> List(int applicationId);
        List<Domains.Entities.General.Tag> FindTagsByTypeId(int applicationId, int typeId);

        // Throws (SingleAsync) unless the tag exists, is not soft-deleted, and belongs to
        // applicationId - missing, cross-application, and soft-deleted are indistinguishable.
        Task<Domains.Entities.General.Tag> GetByIdForApplication(int id, int applicationId);
    }
}