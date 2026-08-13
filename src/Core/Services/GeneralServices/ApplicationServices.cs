using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.GeneralDtos;
using Domains.Entities.General;
using Application.Repository;

namespace Services.GeneralServices
{
    public class ApplicationServices : IApplicationServices
    {
        // fields
        private readonly global::Application.GeneralRepository.IApplicationRepository _applicationRepository;
        private readonly IRepository<ApplicationSetting> _applicationSettingRepository;
        private readonly IMapper _mapper;

        // cunstructor
        public ApplicationServices(global::Application.GeneralRepository.IApplicationRepository applicationRepository, IRepository<ApplicationSetting> applicationSettingRepository, IMapper mapper)
        {
            _applicationSettingRepository = applicationSettingRepository;
            _applicationRepository = applicationRepository;
            _mapper = mapper;
        }

        // methods
        public async Task<ApplicationDto> Create(ApplicationDto application)
        {
            var _application = await _applicationRepository.Create(_mapper.Map<Domains.Entities.General.Application>(application));
            return _mapper.Map<ApplicationDto>(_application);
        }
        public async Task<ApplicationDto> Update(ApplicationDto application)
        {
            var _application = await _applicationRepository.Update(_mapper.Map<Domains.Entities.General.Application>(application));
            return _mapper.Map<ApplicationDto>(_application);
        }
        public async Task Delete(int id)
        {
            await _applicationRepository.Delete(id);
        }
        public async Task<ApplicationDto> GetById(int id)
        {
            var _application = await _applicationRepository.GetById(id);
            return _mapper.Map<ApplicationDto>(_application);
        }
        public List<ApplicationDto> List()
        {
            return _mapper.Map<List<ApplicationDto>>(_applicationRepository.List());
        }

        public async Task<List<UserInApplicationDto>> GetUserApplications(string email)
        {
            return _mapper.Map<List<UserInApplicationDto>>(await _applicationRepository.GetUserApplications(email));
        }

        public async Task AddUserToApplication(string userId, int applicationId)
        {
            await _applicationRepository.AddUserToApplication(userId, applicationId);
        }

        public async Task RemoveUserFromApplication(int relationId)
        {
            await _applicationRepository.RemoveUserFromApplication(relationId);
        }

        public async Task<ApplicationSettingDto> CreateApplicationSetting(ApplicationSettingDto applicationSetting)
        {
            applicationSetting.IsActive = true;
            return _mapper.Map<ApplicationSettingDto>(await _applicationSettingRepository.Create(_mapper.Map<ApplicationSetting>(applicationSetting)));
        }

        public List<ApplicationSettingDto> GetApplicationSetting(int applicationId, int settingId = 0)
        {
            return _mapper.Map<List<ApplicationSettingDto>>(_applicationRepository.GetApplicationSetting(applicationId, settingId));
        }
    }
}
