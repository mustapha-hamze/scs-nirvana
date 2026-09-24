using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.UserManagement;

namespace Application.UseCases.UserManagementServices
{
    public interface IUserManagementServices
    {
        Task<List<UserDto>> List(bool isAdminUser, string email = "", CancellationToken cancellationToken = default);
        Task<UserDto> GetUserByEmailAddress(string email, CancellationToken cancellationToken = default);
        Task<string> GetUserAccesses(string email, int appId, CancellationToken cancellationToken = default);
        // isSuperAdmin: see ITenantAccessGuard.HasAccessAsync - defaults to false so existing
        // callers keep today's membership-only selection rule unchanged.
        Task SetCurrentApplicationId(string email, int appId, bool isSuperAdmin = false, CancellationToken cancellationToken = default);
        Task SetUserAccesses(string accesses, string userId, int appId, CancellationToken cancellationToken = default);
    }
}