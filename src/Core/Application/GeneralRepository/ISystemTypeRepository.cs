using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;
using Application.Repository;

namespace Application.GeneralRepository
{
    public interface ISystemTypeRepository : IRepository<SystemType>
    {
        Task<List<SystemType>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<List<SystemType>> GetTypesInTypeGroup(int applicationId, int typeGroup, CancellationToken cancellationToken = default);
    }
}