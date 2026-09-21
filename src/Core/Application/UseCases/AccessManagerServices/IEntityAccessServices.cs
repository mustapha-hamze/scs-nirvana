using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.AccessManagement;

namespace Application.UseCases.AccessManagerServices
{
    public interface IEntityAccessServices
    {
        Task Create(EntityAccessDto access, int applicationId);
        Task Update(EntityAccessDto access, int applicationId);
        List<EntityAccessDto> List(int applicationId);
        Task<EntityAccessDto> GetById(int id, int applicationId);
        List<EntityAccessDto> GetEntityAccesses(int entityId);
    }
}