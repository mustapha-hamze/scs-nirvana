using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.Tenancy;
using Application.Contracts.UserManagement;
using Application.UserManagementRepository;
using Application.UnitOfWork;

namespace Application.UseCases.UserManagementServices
{
    public class UserManagementServices : IUserManagementServices
    {
        private readonly IUserManagementRepository _userManagementRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentApplicationContext _currentApplicationContext;
        private readonly ITenantAccessGuard _tenantAccessGuard;

        public UserManagementServices(IUserManagementRepository userManagementRepository,
            IUnitOfWork unitOfWork, ICurrentApplicationContext currentApplicationContext,
            ITenantAccessGuard tenantAccessGuard)
        {
            _userManagementRepository = userManagementRepository;
            _unitOfWork = unitOfWork;
            _currentApplicationContext = currentApplicationContext;
            _tenantAccessGuard = tenantAccessGuard;
        }
        public Task<List<UserDto>> List(bool isAdminUser, string email = "", CancellationToken cancellationToken = default)
        {
            return _userManagementRepository.List(isAdminUser, email, cancellationToken);
        }

        public Task<UserDto> GetUserByEmailAddress(string email, CancellationToken cancellationToken = default)
        {
            return _userManagementRepository.GetUserByEmailAddress(email, cancellationToken);
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
            if (appId != 0 && !await _tenantAccessGuard.HasAccessAsync(email, appId, cancellationToken))
                throw new KeyNotFoundException();

            // Stored in the caller's session-scoped context, not persisted on the user record -
            // a selection here must never be visible to another session for the same account.
            _currentApplicationContext.CurrentApplicationId = appId == 0 ? null : appId;
        }
    }
}