using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.General;

namespace Services.GeneralServices
{
    public interface ITagServices
    {
        Task<TagDto> Create(TagDto tag, int applicationId);
        Task Delete(int id, int applicationId);
        List<TagDto> List(int applicationId);
        List<TagDto> FindTagsByTypeId(int applicationId, int typeId);
        Task<TagDto> GetById(int id, int applicationId);
    }
}