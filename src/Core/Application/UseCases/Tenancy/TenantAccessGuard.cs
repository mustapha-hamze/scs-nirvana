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

        // True when applicationId is active/non-deleted and either the caller is a SuperAdmin, or
        // the identified user exists and has an active/non-deleted membership for it. A missing
        // user, missing/revoked membership, or inactive/deleted application all collapse to the
        // same false - deliberately indistinguishable to callers - except that SuperAdmin never
        // needs a membership row in the first place.
        public async Task<bool> HasAccessAsync(string email, int applicationId, bool isSuperAdmin = false, CancellationToken cancellationToken = default)
        {
            var applicationIsUsable = await _applicationRepository.ExistsActiveApplication(applicationId, cancellationToken);
            if (!applicationIsUsable)
                return false;

            if (isSuperAdmin)
                return true;

            var user = await _userManagementRepository.GetUserByEmailAddress(email, cancellationToken);
            return user != null && await _userManagementRepository.HasActiveMembership(user.Id, applicationId, cancellationToken);
        }
    }
}
