using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;
using Application.Repository;

namespace Application.GeneralRepository
{
    public interface IApplicationRepository : IRepository<Domains.Entities.General.Application>
    {
        List<Domains.Entities.General.Application> List();
        Task<List<UserInApplication>> GetUserApplications(string email);
        Task AddUserToApplication(string userId, int applicationId);
        Task RemoveUserFromApplication(int relationId);
        List<ApplicationSetting> GetApplicationSetting(int applicationId, int settingId = 0);
    }
}