using Application.Contracts.Tenancy;
using Application.GeneralRepository;
using Application.UserManagementRepository;

namespace Application.UseCases.Tenancy
{
    public class TenantAccessGuard : ITenantAccessGuard
    {
        private readonly IUserManagementRepository _userManagementRepository;
        private readonly IApplicationRepository _applicationRepository;

        public TenantAccessGuard(IUserManagementRepository userManagementRepository, IApplicationRepository applicationRepository)
        {
            _userManagementRepository = userManagementRepository;
            _applicationRepository = applicationRepository;
        }

        // True only when the identified user exists, has an active/non-deleted membership for
        // applicationId, and applicationId itself is active/non-deleted. A missing user,
        // membership, or application all collapse to the same false - deliberately
        // indistinguishable to callers.
        public async Task<bool> HasAccessAsync(string email, int applicationId, CancellationToken cancellationToken = default)
        {
            var user = await _userManagementRepository.GetUserByEmailAddress(email, cancellationToken);
            var isMember = user != null && await _userManagementRepository.HasActiveMembership(user.Id, applicationId, cancellationToken);
            var applicationIsUsable = await _applicationRepository.ExistsActiveApplication(applicationId, cancellationToken);

            return isMember && applicationIsUsable;
        }
    }
}
