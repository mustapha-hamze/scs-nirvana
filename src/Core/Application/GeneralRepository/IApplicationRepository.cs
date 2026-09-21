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

        // Scoped to applicationId so a member of one application can't remove a membership row
        // (relationId) that belongs to a different application just by guessing/enumerating ids.
        Task RemoveUserFromApplication(int relationId, int applicationId);
        List<ApplicationSetting> GetApplicationSetting(int applicationId, int settingId = 0);
        Task<ApplicationSetting> CreateApplicationSetting(ApplicationSetting setting);

        // True only when the application exists, is active, and is not soft-deleted.
        Task<bool> ExistsActiveApplication(int applicationId);
    }
}