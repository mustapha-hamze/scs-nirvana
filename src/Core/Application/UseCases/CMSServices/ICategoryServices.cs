using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;

namespace Application.UseCases.CMSServices
{
    public interface ICategoryServices
    {
        Task<CategoryDto> Create(CategoryDto category, int applicationId, CancellationToken cancellationToken = default);
        Task Delete(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<CategoryDto>> List(int applicationId, CancellationToken cancellationToken = default);
        Task<CategoryDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default);
        Task<List<CategoryDto>> GetAllFullPath(int applicationId, CancellationToken cancellationToken = default);
    }
}