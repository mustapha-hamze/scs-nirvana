using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Dto.GeneralDtos;

namespace Services.GeneralServices
{
    public interface ICultureServices
    {
        Task<CultureDto> Create(CultureDto zone);
        Task Delete(int id);
        List<CultureDto> List();
        Task<CultureDto> GetById(int id);
    }
}