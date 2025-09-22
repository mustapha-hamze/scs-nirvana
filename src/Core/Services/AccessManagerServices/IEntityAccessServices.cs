using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Dto.AccessManagerDtos;

namespace Services.AccessManagerServices
{
    public interface IEntityAccessServices
    {
        Task Create(EntityAccessDto access);
        Task Update(EntityAccessDto access);
        List<EntityAccessDto> List(int applicationId);
        Task<EntityAccessDto> GetById(int id);
        List<EntityAccessDto> GetEntityAccesses(int entityId);
    }
}