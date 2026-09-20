using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.General;
using Application.GeneralRepository;
using Application.Contracts.General;
using Application.UnitOfWork;

namespace Services.GeneralServices
{
    public class SystemTypeServices : ISystemTypeServices
    {
        private readonly ISystemTypeRepository _systemTypeRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        public SystemTypeServices(ISystemTypeRepository systemTypeRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _systemTypeRepository = systemTypeRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task Create(SystemTypeDto systemType)
        {
            await _systemTypeRepository.Create(_mapper.Map<SystemType>(systemType));
            await _unitOfWork.SaveChangesAsync();
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
