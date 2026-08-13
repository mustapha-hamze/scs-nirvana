using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.General;

namespace Services.GeneralServices
{
    public interface IApplicationServices
    {
        Task<ApplicationDto> Create(ApplicationDto application);
        Task<ApplicationDto> Update(ApplicationDto application);
        Task Delete(int id);
        Task<ApplicationDto> GetById(int id);
        List<ApplicationDto> List();
        Task<List<UserInApplicationDto>> GetUserApplications(string email);
        Task AddUserToApplication(string userId, int applicationId);
        Task RemoveUserFromApplication(int relationId);
        Task<ApplicationSettingDto> CreateApplicationSetting(ApplicationSettingDto applicationSetting);
        List<ApplicationSettingDto> GetApplicationSetting(int applicationId, int settingId = 0);
    }
}