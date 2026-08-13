using System.Collections.Generic;
using Domains.Entities.General;
using Application.Repository;

namespace Application.GeneralRepository
{
    public interface ISystemTypeRepository : IRepository<SystemType>
    {
        List<SystemType> List(int applicationId);
        List<SystemType> GetTypesInTypeGroup(int applicationId, int typeGroup);
    }
}