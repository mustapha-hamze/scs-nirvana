using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;
using Application.Repository;

namespace Application.GeneralRepository
{
    public interface ICultureRepository : IRepository<Culture>
    {
        Task<List<Culture>> List(CancellationToken cancellationToken = default);
    }
}