using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.General;

namespace Application.UseCases.GeneralServices
{
    public interface ISystemTypeServices
    {
        Task Create(SystemTypeDto systemType, CancellationToken cancellationToken = default);

        Task<List<SystemTypeDto>> List(int applicationId, CancellationToken cancellationToken = default);

        Task<List<SystemTypeDto>> GetTypesInTypeGroup(int applicationId, int typeGroup, CancellationToken cancellationToken = default);
    }
}