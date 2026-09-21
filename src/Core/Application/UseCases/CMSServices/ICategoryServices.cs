using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;

namespace Application.UseCases.CMSServices
{
    public interface ICategoryServices
    {
        Task<CategoryDto> Create(CategoryDto category, int applicationId);
        Task Delete(int id, int applicationId);
        List<CategoryDto> List(int applicationId);
        Task<CategoryDto> GetById(int id, int applicationId);
        List<CategoryDto> GetAllFullPath(int applicationId);
    }
}