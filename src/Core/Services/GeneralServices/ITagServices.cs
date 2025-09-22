using System.Collections.Generic;
using System.Threading.Tasks;
using Infrastructure.Dto.GeneralDtos;

namespace Services.GeneralServices
{
    public interface ITagServices
    {
        Task<TagDto> Create(TagDto tag);
        Task Delete(int id);
        List<TagDto> List(int applicationId);
        List<TagDto> FindTagsByTypeId(int applicationId, int typeId);
        Task<TagDto> GetById(int id);
    }
}