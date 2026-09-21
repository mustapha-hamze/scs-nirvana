using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.General;

namespace Application.UseCases.GeneralServices
{
    public interface ISystemTypeServices
    {
        Task Create(SystemTypeDto systemType);

        List<SystemTypeDto> List(int applicationId);

        List<SystemTypeDto> GetTypesInTypeGroup(int applicationId, int typeGroup);
    }
}