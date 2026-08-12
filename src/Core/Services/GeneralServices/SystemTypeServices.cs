using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.General;
using Infrastructure;
using Application.GeneralRepository;
using Infrastructure.Dto.GeneralDtos;

namespace Services.GeneralServices
{
    public class SystemTypeServices : ISystemTypeServices
    {
        private readonly ISystemTypeRepository _systemTypeRepository;
        private readonly IMapper _mapper;

        public SystemTypeServices(ISystemTypeRepository systemTypeRepository, IMapper mapper)
        {
            _systemTypeRepository = systemTypeRepository;
            _mapper = mapper;
        }

        public async Task Create(SystemTypeDto systemType)
        {
            await _systemTypeRepository.Create(_mapper.Map<SystemType>(systemType));
        }

        public List<SystemTypeDto> List(int applicationId)
        {
            return _mapper.Map<List<SystemTypeDto>>(_systemTypeRepository.List(applicationId));
        }

        public List<SystemTypeDto> GetTypesInTypeGroup(int applicationId, int typeGroup)
        {
            return _mapper.Map<List<SystemTypeDto>>(_systemTypeRepository.GetTypesInTypeGroup(applicationId, typeGroup));
        }
    }
}
