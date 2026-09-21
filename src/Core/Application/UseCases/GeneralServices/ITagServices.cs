using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.General;

namespace Application.UseCases.GeneralServices
{
    public interface ITagServices
    {
        Task<TagDto> Create(TagDto tag, int applicationId, CancellationToken cancellationToken = default);
        Task Delete(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<TagDto>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<List<TagDto>> FindTagsByTypeId(int applicationId, int typeId, CancellationToken cancellationToken = default);
        Task<TagDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default);
    }
}