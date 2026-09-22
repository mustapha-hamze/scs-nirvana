using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.General;

namespace Application.UseCases.GeneralServices
{
    public interface IApplicationServices
    {
        Task<ApplicationDto> Create(ApplicationDto application, CancellationToken cancellationToken = default);
        Task<ApplicationDto> Update(ApplicationDto application, CancellationToken cancellationToken = default);
        Task Delete(int id, CancellationToken cancellationToken = default);
        Task<ApplicationDto> GetById(int id, CancellationToken cancellationToken = default);
        Task<List<ApplicationDto>> List(CancellationToken cancellationToken = default);
        Task<List<UserInApplicationDto>> GetUserApplications(string email, CancellationToken cancellationToken = default);
        Task AddUserToApplication(string userId, int applicationId, CancellationToken cancellationToken = default);
        Task RemoveUserFromApplication(int relationId, int applicationId, CancellationToken cancellationToken = default);
        Task<ApplicationSettingDto> CreateApplicationSetting(ApplicationSettingDto applicationSetting, int applicationId, CancellationToken cancellationToken = default);
        Task<List<ApplicationSettingDto>> GetApplicationSetting(int applicationId, int settingId = 0, CancellationToken cancellationToken = default);
    }
}