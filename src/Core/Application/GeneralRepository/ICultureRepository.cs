using System.Collections.Generic;
using Domains.Entities.General;
using Application.Repository;

namespace Application.GeneralRepository
{
    public interface ICultureRepository : IRepository<Culture>
    {
        List<Culture> List();
    }
}