using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.General;
using Domains.Entities.General;
using Application.UnitOfWork;

namespace Application.UseCases.GeneralServices
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
        public async Task<ApplicationDto> Create(ApplicationDto application, CancellationToken cancellationToken = default)
        {
            var _application = await _applicationRepository.Create(_mapper.Map<Domains.Entities.General.Application>(application));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<ApplicationDto>(_application);
        }
        public async Task<ApplicationDto> Update(ApplicationDto application, CancellationToken cancellationToken = default)
        {
            var _application = await _applicationRepository.Update(_mapper.Map<Domains.Entities.General.Application>(application));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<ApplicationDto>(_application);
        }
        public async Task Delete(int id, CancellationToken cancellationToken = default)
        {
            await _applicationRepository.Delete(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        public async Task<ApplicationDto> GetById(int id, CancellationToken cancellationToken = default)
        {
            var _application = await _applicationRepository.GetById(id, cancellationToken);
            return _mapper.Map<ApplicationDto>(_application);
        }
        public async Task<List<ApplicationDto>> List(CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<ApplicationDto>>(await _applicationRepository.List(cancellationToken));
        }

        public async Task<List<UserInApplicationDto>> GetUserApplications(string email, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<UserInApplicationDto>>(await _applicationRepository.GetUserApplications(email, cancellationToken));
        }

        public async Task AddUserToApplication(string userId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _applicationRepository.AddUserToApplication(userId, applicationId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveUserFromApplication(int relationId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _applicationRepository.RemoveUserFromApplication(relationId, applicationId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<ApplicationSettingDto> CreateApplicationSetting(ApplicationSettingDto applicationSetting, int applicationId, CancellationToken cancellationToken = default)
        {
            applicationSetting.IsActive = true;
            // Never trust ApplicationId from the DTO - server-pin it.
            applicationSetting.ApplicationId = applicationId;
            var created = await _applicationRepository.CreateApplicationSetting(_mapper.Map<ApplicationSetting>(applicationSetting));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<ApplicationSettingDto>(created);
        }

        public async Task<List<ApplicationSettingDto>> GetApplicationSetting(int applicationId, int settingId = 0, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<ApplicationSettingDto>>(await _applicationRepository.GetApplicationSetting(applicationId, settingId, cancellationToken));
        }
    }
}