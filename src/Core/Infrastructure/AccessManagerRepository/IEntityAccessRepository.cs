using System.Collections.Generic;
using Domains.Entities.AccessManagement;
using Infrastructure.Repository;

namespace Infrastructure.AccessManagerRepository
{
    public interface IEntityAccessRepository : IRepository<EntityAccess>
    {
        List<EntityAccess> List(int applicationId);

        List<EntityAccess> GetEntityAccesses(int entityId);
    }
}