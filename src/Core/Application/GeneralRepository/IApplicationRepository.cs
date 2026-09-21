using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.General;

namespace Application.GeneralRepository
{
    public interface IApplicationRepository
    {
        // These operate on the Application aggregate root itself, so there's no narrower
        // "application scope" to require - the id already identifies the tenant.
        Task<Domains.Entities.General.Application> Create(Domains.Entities.General.Application application);
        Task<Domains.Entities.General.Application> Update(Domains.Entities.General.Application application);
        Task Delete(int id, CancellationToken cancellationToken = default);
        Task<Domains.Entities.General.Application> GetById(int id, CancellationToken cancellationToken = default);

        Task<List<Domains.Entities.General.Application>> List(CancellationToken cancellationToken = default);
        Task<List<UserInApplication>> GetUserApplications(string email, CancellationToken cancellationToken = default);
        Task AddUserToApplication(string userId, int applicationId, CancellationToken cancellationToken = default);

        // Scoped to applicationId so a member of one application can't remove a membership row
        // (relationId) that belongs to a different application just by guessing/enumerating ids.
        Task RemoveUserFromApplication(int relationId, int applicationId, CancellationToken cancellationToken = default);
        Task<List<ApplicationSetting>> GetApplicationSetting(int applicationId, int settingId = 0, CancellationToken cancellationToken = default);
        Task<ApplicationSetting> CreateApplicationSetting(ApplicationSetting setting);

        // True only when the application exists, is active, and is not soft-deleted.
        Task<bool> ExistsActiveApplication(int applicationId, CancellationToken cancellationToken = default);
    }
}