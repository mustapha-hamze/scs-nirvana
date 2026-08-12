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
        public UserManagementServices(IUserManagementRepository userManagementRepository)
        {
            _userManagementRepository = userManagementRepository;
        }
        public List<UserDto> List(bool isAdminUser, string email = "")
        {
            return Mapper(_userManagementRepository.List(isAdminUser, email));
        }

        public UserDto GetUserByEmailAddress(string email)
        {
            return Mapper(_userManagementRepository.GetUserByEmailAddress(email));
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

        /// Mapper

        private UserDto Mapper(ApplicationUser applicationUser)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ApplicationUser, UserDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ApplicationUser, UserDto>(applicationUser);
        }

        private List<UserDto> Mapper(List<ApplicationUser> applicationUsers)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ApplicationUser, UserDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<ApplicationUser>, List<UserDto>>(applicationUsers);
        }
    }
}