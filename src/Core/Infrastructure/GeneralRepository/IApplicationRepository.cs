using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;
using Infrastructure.Repository;

namespace Infrastructure.GeneralRepository
{
    public interface IApplicationRepository : IRepository<Application>
    {
        List<Application> List();
        Task<List<UserInApplication>> GetUserApplications(string email);
        Task AddUserToApplication(string userId, int applicationId);
        Task RemoveUserFromApplication(int relationId);
        List<ApplicationSetting> GetApplicationSetting(int applicationId, int settingId = 0);
    }
}