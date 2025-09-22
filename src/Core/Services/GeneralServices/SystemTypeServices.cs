using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.General;
using Infrastructure;
using Infrastructure.GeneralRepository;
using Infrastructure.Dto.GeneralDtos;

namespace Services.GeneralServices
{
    public class SystemTypeServices : ISystemTypeServices
    {
        private readonly ISystemTypeRepository _systemTypeRepository;
        public SystemTypeServices(ISystemTypeRepository systemTypeRepository)
        {
            _systemTypeRepository = systemTypeRepository;
        }

        public async Task Create(SystemTypeDto systemType)
        {
            await _systemTypeRepository.Create(Mapper(systemType));
        }

        public List<SystemTypeDto> List(int applicationId)
        {
            return Mapper(_systemTypeRepository.List(applicationId));
        }

        public List<SystemTypeDto> GetTypesInTypeGroup(int applicationId, int typeGroup)
        {
            return Mapper(_systemTypeRepository.GetTypesInTypeGroup(applicationId, typeGroup));
        }

        // mapper
        private SystemType Mapper(SystemTypeDto systemType)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SystemTypeDto, SystemType>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SystemTypeDto, SystemType>(systemType);
        }

        private SystemTypeDto Mapper(SystemType systemType)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SystemType, SystemTypeDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SystemType, SystemTypeDto>(systemType);
        }

        private List<SystemTypeDto> Mapper(List<SystemType> systemType)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SystemType, SystemTypeDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<SystemType>, List<SystemTypeDto>>(systemType);
        }
    }
}