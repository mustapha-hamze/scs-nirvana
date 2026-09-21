using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.UserManagement;

namespace Application.UseCases.UserManagementServices
{
    public interface IUserManagementServices
    {
        List<UserDto> List(bool isAdminUser, string email = "");
        UserDto GetUserByEmailAddress(string email);
        Task<string> GetUserAccesses(string email);
        Task<string> GetUserAccesses(string email, int appId);
        Task SetCurrentApplicationId(string email, int appId);
        Task SetUserAccesses(string accesses, string userId, int appId);
    }
}