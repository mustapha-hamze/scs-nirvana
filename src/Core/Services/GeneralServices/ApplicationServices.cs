using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.GeneralDtos;
using Domains.Entities.General;
using Infrastructure;
using Application.Repository;

namespace Services.GeneralServices
{
    public class ApplicationServices : IApplicationServices
    {
        // fields
        private readonly global::Application.GeneralRepository.IApplicationRepository _applicationRepository;
        private readonly IRepository<ApplicationSetting> _applicationSettingRepository;

        // cunstructor
        public ApplicationServices(global::Application.GeneralRepository.IApplicationRepository applicationRepository, IRepository<ApplicationSetting> applicationSettingRepository)
        {
            _applicationSettingRepository = applicationSettingRepository;
            _applicationRepository = applicationRepository;
        }

        // methods
        public async Task<ApplicationDto> Create(ApplicationDto application)
        {
            var _application = await _applicationRepository.Create(Mapper(application));
            return Mapper(_application);
        }
        public async Task<ApplicationDto> Update(ApplicationDto application)
        {
            var _application = await _applicationRepository.Update(Mapper(application));
            return Mapper(_application);
        }
        public async Task Delete(int id)
        {
            await _applicationRepository.Delete(id);
        }
        public async Task<ApplicationDto> GetById(int id)
        {
            var _application = await _applicationRepository.GetById(id);
            return Mapper(_application);
        }
        public List<ApplicationDto> List()
        {
            return Mapper(_applicationRepository.List());
        }

        public async Task<List<UserInApplicationDto>> GetUserApplications(string email)
        {
            return Mapper(await _applicationRepository.GetUserApplications(email));
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
            return Mapper(await _applicationSettingRepository.Create(Mapper(applicationSetting)));
        }

        public List<ApplicationSettingDto> GetApplicationSetting(int applicationId, int settingId = 0)
        {
            return Mapper(_applicationRepository.GetApplicationSetting(applicationId, settingId));
        }

        // auto mappers
        private List<ApplicationDto> Mapper(List<Domains.Entities.General.Application> applications)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Domains.Entities.General.Application, ApplicationDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<Domains.Entities.General.Application>, List<ApplicationDto>>(applications);
        }

        private ApplicationDto Mapper(Domains.Entities.General.Application application)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Domains.Entities.General.Application, ApplicationDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<Domains.Entities.General.Application, ApplicationDto>(application);
        }

        private Domains.Entities.General.Application Mapper(ApplicationDto application)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ApplicationDto, Domains.Entities.General.Application>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ApplicationDto, Domains.Entities.General.Application>(application);
        }

        private List<UserInApplicationDto> Mapper(List<UserInApplication> userApplications)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<UserInApplication, UserInApplicationDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<UserInApplication>, List<UserInApplicationDto>>(userApplications);
        }

        private ApplicationSettingDto Mapper(ApplicationSetting applicationSetting)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ApplicationSetting, ApplicationSettingDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ApplicationSetting, ApplicationSettingDto>(applicationSetting);
        }

        private ApplicationSetting Mapper(ApplicationSettingDto applicationSetting)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ApplicationSettingDto, ApplicationSetting>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ApplicationSettingDto, ApplicationSetting>(applicationSetting);
        }

        private List<ApplicationSettingDto> Mapper(List<ApplicationSetting> applicationSetting)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ApplicationSetting, ApplicationSettingDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<ApplicationSetting>, List<ApplicationSettingDto>>(applicationSetting);
        }
    }
}