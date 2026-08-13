using Domains.Entities.User;

namespace Application.UserManagementRepository
{
    public interface IUserManagementRepository
    {
        List<ApplicationUser> List(bool isAdminUser, string email);
        ApplicationUser GetUserByEmailAddress(string email);
        Task<string> GetUserAccesses(string email);
        Task<string> GetUserAccesses(string email, int appId);
        Task SetCurrentApplicationId(string email, int appId);
        Task SetUserAccesses(string accesses, string userId, int appId);
    }
}