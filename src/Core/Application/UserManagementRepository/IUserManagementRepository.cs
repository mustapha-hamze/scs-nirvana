using Application.Contracts.UserManagement;

namespace Application.UserManagementRepository
{
    public interface IUserManagementRepository
    {
        Task<List<UserDto>> List(bool isAdminUser, string email, CancellationToken cancellationToken = default);
        Task<UserDto> GetUserByEmailAddress(string email, CancellationToken cancellationToken = default);
        Task<string> GetUserAccesses(string email, int appId, CancellationToken cancellationToken = default);
        Task SetUserAccesses(string accesses, string userId, int appId, CancellationToken cancellationToken = default);

        // True only when the user has an active, non-deleted UserInApplication row for this
        // application.
        Task<bool> HasActiveMembership(string userId, int applicationId, CancellationToken cancellationToken = default);
    }
}