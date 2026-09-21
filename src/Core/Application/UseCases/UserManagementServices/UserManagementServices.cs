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
        public Task<List<UserDto>> List(bool isAdminUser, string email = "", CancellationToken cancellationToken = default)
        {
            return _userManagementRepository.List(isAdminUser, email, cancellationToken);
        }

        public Task<UserDto> GetUserByEmailAddress(string email, CancellationToken cancellationToken = default)
        {
            return _userManagementRepository.GetUserByEmailAddress(email, cancellationToken);
        }

        public async Task<string> GetUserAccesses(string email, CancellationToken cancellationToken = default)
        {
            return await _userManagementRepository.GetUserAccesses(email, cancellationToken);
        }

        public async Task SetUserAccesses(string accesses, string userId, int appId, CancellationToken cancellationToken = default)
        {
            await _userManagementRepository.SetUserAccesses(accesses, userId, appId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<string> GetUserAccesses(string email, int appId, CancellationToken cancellationToken = default)
        {
            return await _userManagementRepository.GetUserAccesses(email, appId, cancellationToken);
        }

        public async Task SetCurrentApplicationId(string email, int appId, CancellationToken cancellationToken = default)
        {
            // 0 is the existing logout/clear-selection flow and is always allowed. Any other
            // value must be a real, active, non-deleted application that the identified user has
            // an active, non-deleted membership for - a missing, unauthorized, or deleted target
            // is rejected identically, so none of those cases is distinguishable to the caller.
            if (appId != 0)
            {
                var user = await _userManagementRepository.GetUserByEmailAddress(email, cancellationToken);
                var isMember = user != null && await _userManagementRepository.HasActiveMembership(user.Id, appId, cancellationToken);
                var applicationIsUsable = await _applicationRepository.ExistsActiveApplication(appId, cancellationToken);

                if (!isMember || !applicationIsUsable)
                    throw new KeyNotFoundException();
            }

            await _userManagementRepository.SetCurrentApplicationId(email, appId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}