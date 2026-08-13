using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.CMS;

namespace Services.CMSServices
{
    public interface ICategoryServices
    {
        Task<CategoryDto> Create(CategoryDto category);
        Task Delete(int id);
        List<CategoryDto> List(int applicationId);
        Task<CategoryDto> GetById(int id);
        List<CategoryDto> GetAllFullPath(int applicationId);
    }
}