using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.General;
using Application.GeneralRepository;
using Application.Contracts.General;
using Application.UnitOfWork;

namespace Application.UseCases.GeneralServices
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

        public async Task Create(SystemTypeDto systemType, CancellationToken cancellationToken = default)
        {
            await _systemTypeRepository.Create(_mapper.Map<SystemType>(systemType));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<SystemTypeDto>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<SystemTypeDto>>(await _systemTypeRepository.List(applicationId, cancellationToken));
        }

        public async Task<List<SystemTypeDto>> GetTypesInTypeGroup(int applicationId, int typeGroup, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<SystemTypeDto>>(await _systemTypeRepository.GetTypesInTypeGroup(applicationId, typeGroup, cancellationToken));
        }
    }
}