using System.Collections.Generic;
using Domains.Entities.General;
using Application.Repository;

namespace Application.GeneralRepository
{
    public interface ITagRepository : IRepository<Domains.Entities.General.Tag>
    {
        List<Domains.Entities.General.Tag> List(int applicationId);
        List<Domains.Entities.General.Tag> FindTagsByTypeId(int applicationId, int typeId);
    }
}