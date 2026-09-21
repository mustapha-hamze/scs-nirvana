using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.UserManagement;
using Application.GeneralRepository;
using Application.UserManagementRepository;
using Application.UnitOfWork;

namespace Application.UseCases.UserManagementServices
{
    public class UserManagementServices : IUserManagementServices
    {
        private readonly IUserManagementRepository _userManagementRepository;
        private readonly IApplicationRepository _applicationRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UserManagementServices(IUserManagementRepository userManagementRepository,
            IApplicationRepository applicationRepository, IUnitOfWork unitOfWork)
        {
            _userManagementRepository = userManagementRepository;
            _applicationRepository = applicationRepository;
            _unitOfWork = unitOfWork;
        }
        public List<UserDto> List(bool isAdminUser, string email = "")
        {
            return _userManagementRepository.List(isAdminUser, email);
        }

        public UserDto GetUserByEmailAddress(string email)
        {
            return _userManagementRepository.GetUserByEmailAddress(email);
        }

        public async Task<string> GetUserAccesses(string email)
        {
            return await _userManagementRepository.GetUserAccesses(email);
        }

        public async Task SetUserAccesses(string accesses, string userId, int appId)
        {
            await _userManagementRepository.SetUserAccesses(accesses, userId, appId);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<string> GetUserAccesses(string email, int appId)
        {
            return await _userManagementRepository.GetUserAccesses(email, appId);
        }

        public async Task SetCurrentApplicationId(string email, int appId)
        {
            // 0 is the existing logout/clear-selection flow and is always allowed. Any other
            // value must be a real, active, non-deleted application that the identified user has
            // an active, non-deleted membership for - a missing, unauthorized, or deleted target
            // is rejected identically, so none of those cases is distinguishable to the caller.
            if (appId != 0)
            {
                var user = _userManagementRepository.GetUserByEmailAddress(email);
                var isMember = user != null && await _userManagementRepository.HasActiveMembership(user.Id, appId);
                var applicationIsUsable = await _applicationRepository.ExistsActiveApplication(appId);

                if (!isMember || !applicationIsUsable)
                    throw new KeyNotFoundException();
            }

            await _userManagementRepository.SetCurrentApplicationId(email, appId);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
