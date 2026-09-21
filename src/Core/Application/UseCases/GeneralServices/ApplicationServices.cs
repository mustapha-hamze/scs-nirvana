using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.General;
using Domains.Entities.General;
using Application.UnitOfWork;

namespace Services.GeneralServices
{
    public class ApplicationServices : IApplicationServices
    {
        // fields
        private readonly global::Application.GeneralRepository.IApplicationRepository _applicationRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // cunstructor
        public ApplicationServices(global::Application.GeneralRepository.IApplicationRepository applicationRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _applicationRepository = applicationRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task<ApplicationDto> Create(ApplicationDto application)
        {
            var _application = await _applicationRepository.Create(_mapper.Map<Domains.Entities.General.Application>(application));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<ApplicationDto>(_application);
        }
        public async Task<ApplicationDto> Update(ApplicationDto application)
        {
            var _application = await _applicationRepository.Update(_mapper.Map<Domains.Entities.General.Application>(application));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<ApplicationDto>(_application);
        }
        public async Task Delete(int id)
        {
            await _applicationRepository.Delete(id);
            await _unitOfWork.SaveChangesAsync();
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
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task RemoveUserFromApplication(int relationId, int applicationId)
        {
            await _applicationRepository.RemoveUserFromApplication(relationId, applicationId);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<ApplicationSettingDto> CreateApplicationSetting(ApplicationSettingDto applicationSetting, int applicationId)
        {
            applicationSetting.IsActive = true;
            // Never trust ApplicationId from the DTO - server-pin it.
            applicationSetting.ApplicationId = applicationId;
            var created = await _applicationRepository.CreateApplicationSetting(_mapper.Map<ApplicationSetting>(applicationSetting));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<ApplicationSettingDto>(created);
        }

        public List<ApplicationSettingDto> GetApplicationSetting(int applicationId, int settingId = 0)
        {
            return _mapper.Map<List<ApplicationSettingDto>>(_applicationRepository.GetApplicationSetting(applicationId, settingId));
        }
    }
}
