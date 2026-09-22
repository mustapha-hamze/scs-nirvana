using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.General;

namespace Application.UseCases.GeneralServices
{
    public interface ICultureServices
    {
        Task<CultureDto> Create(CultureDto zone, CancellationToken cancellationToken = default);
        Task Delete(int id, CancellationToken cancellationToken = default);
        Task<List<CultureDto>> List(CancellationToken cancellationToken = default);
        Task<CultureDto> GetById(int id, CancellationToken cancellationToken = default);
    }
}