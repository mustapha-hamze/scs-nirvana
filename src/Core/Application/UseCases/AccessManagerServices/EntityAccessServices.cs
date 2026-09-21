using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.AccessManagement;
using Application.AccessManagerRepository;
using Application.Contracts.AccessManagement;
using Application.UnitOfWork;

namespace Application.UseCases.AccessManagerServices
{
    public class EntityAccessServices : IEntityAccessServices
    {
        private readonly IEntityAccessRepository _entityAccessRepository;
        private readonly ISectorEntityRepository _sectorEntityRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        public EntityAccessServices(IEntityAccessRepository entityAccessRepository, ISectorEntityRepository sectorEntityRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _entityAccessRepository = entityAccessRepository;
            _sectorEntityRepository = sectorEntityRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task Create(EntityAccessDto access, int applicationId, CancellationToken cancellationToken = default)
        {
            // The target SectorEntity must belong to this application before access can be granted on it.
            await _sectorEntityRepository.GetByIdForApplication(access.EntityId, applicationId, cancellationToken);
            await _entityAccessRepository.Create(_mapper.Map<EntityAccess>(access));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task Update(EntityAccessDto access, int applicationId, CancellationToken cancellationToken = default)
        {
            await _entityAccessRepository.GetByIdForApplication(access.Id, applicationId, cancellationToken);
            await _sectorEntityRepository.GetByIdForApplication(access.EntityId, applicationId, cancellationToken);
            await _entityAccessRepository.Update(_mapper.Map<EntityAccess>(access));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<EntityAccessDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<EntityAccessDto>(await _entityAccessRepository.GetByIdForApplication(id, applicationId, cancellationToken));
        }

        public async Task<List<EntityAccessDto>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<EntityAccessDto>>(await _entityAccessRepository.List(applicationId, cancellationToken));
        }
        public async Task<List<EntityAccessDto>> GetEntityAccesses(int entityId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<EntityAccessDto>>(await _entityAccessRepository.GetEntityAccesses(entityId, cancellationToken));
        }
    }
}