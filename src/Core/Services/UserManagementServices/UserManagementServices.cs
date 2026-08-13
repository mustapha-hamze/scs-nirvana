using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.User;
using Infrastructure.Dto.UserManagementDtos;
using Application.UserManagementRepository;
using Dapper;

namespace Services.UserManagementServices
{
    public class UserManagementServices : IUserManagementServices
    {
        private readonly IUserManagementRepository _userManagementRepository;
        private readonly IMapper _mapper;

        public UserManagementServices(IUserManagementRepository userManagementRepository, IMapper mapper)
        {
            _userManagementRepository = userManagementRepository;
            _mapper = mapper;
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
        }

        public async Task<string> GetUserAccesses(string email, int appId)
        {
            return await _userManagementRepository.GetUserAccesses(email, appId);
        }

        public async Task SetCurrentApplicationId(string email, int appId)
        {
            await _userManagementRepository.SetCurrentApplicationId(email, appId);
        }
    }
}
