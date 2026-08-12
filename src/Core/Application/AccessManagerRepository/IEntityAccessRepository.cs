using System.Collections.Generic;
using Domains.Entities.AccessManagement;
using Application.Repository;

namespace Application.AccessManagerRepository
{
    public interface IEntityAccessRepository : IRepository<EntityAccess>
    {
        List<EntityAccess> List(int applicationId);

        List<EntityAccess> GetEntityAccesses(int entityId);
    }
}