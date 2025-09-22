using System.Collections.Generic;
using Domains.Entities.General;
using Infrastructure.Repository;

namespace Infrastructure.GeneralRepository
{
    public interface ISystemTypeRepository : IRepository<SystemType>
    {
        List<SystemType> List(int applicationId);
        List<SystemType> GetTypesInTypeGroup(int applicationId, int typeGroup);
    }
}