using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.User;
using Application.Contracts.UserManagement;
using Application.UserManagementRepository;
using Application.UnitOfWork;

namespace Services.UserManagementServices
{
    public class UserManagementServices : IUserManagementServices
    {
        private readonly IUserManagementRepository _userManagementRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        public UserManagementServices(IUserManagementRepository userManagementRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _userManagementRepository = userManagementRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }
        public List<UserDto> List(bool isAdminUser, string email = "")
        {
            return _mapper.Map<List<UserDto>>(_userManagementRepository.List(isAdminUser, email));
        }

        public UserDto GetUserByEmailAddress(string email)
        {
            return _mapper.Map<UserDto>(_userManagementRepository.GetUserByEmailAddress(email));
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
            await _userManagementRepository.SetCurrentApplicationId(email, appId);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
