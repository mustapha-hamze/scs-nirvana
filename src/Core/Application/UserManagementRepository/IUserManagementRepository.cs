using Application.Contracts.UserManagement;

namespace Application.UserManagementRepository
{
    public interface IUserManagementRepository
    {
        List<UserDto> List(bool isAdminUser, string email);
        UserDto GetUserByEmailAddress(string email);
        Task<string> GetUserAccesses(string email);
        Task<string> GetUserAccesses(string email, int appId);
        Task SetCurrentApplicationId(string email, int appId);
        Task SetUserAccesses(string accesses, string userId, int appId);
    }
}