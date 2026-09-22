using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;

namespace Application.GeneralRepository
{
    public interface ISystemTypeRepository
    {
        Task<SystemType> Create(SystemType systemType);

        Task<List<SystemType>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<List<SystemType>> GetTypesInTypeGroup(int applicationId, int typeGroup, CancellationToken cancellationToken = default);
    }
}