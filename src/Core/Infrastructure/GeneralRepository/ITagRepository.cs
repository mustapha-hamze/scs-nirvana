using System.Collections.Generic;
using Domains.Entities.General;
using Infrastructure.Repository;

namespace Infrastructure.GeneralRepository
{
    public interface ITagRepository : IRepository<Tag>
    {
        List<Tag> List(int applicationId);
        List<Tag> FindTagsByTypeId(int applicationId, int typeId);
    }
}