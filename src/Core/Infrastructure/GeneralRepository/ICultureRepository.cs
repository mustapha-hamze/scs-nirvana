using System.Collections.Generic;
using Domains.Entities.General;
using Infrastructure.Repository;

namespace Infrastructure.GeneralRepository
{
    public interface ICultureRepository : IRepository<Culture>
    {
        List<Culture> List();
    }
}