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

        public async Task Create(EntityAccessDto access, int applicationId)
        {
            // The target SectorEntity must belong to this application before access can be granted on it.
            await _sectorEntityRepository.GetByIdForApplication(access.EntityId, applicationId);
            await _entityAccessRepository.Create(_mapper.Map<EntityAccess>(access));
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task Update(EntityAccessDto access, int applicationId)
        {
            await _entityAccessRepository.GetByIdForApplication(access.Id, applicationId);
            await _sectorEntityRepository.GetByIdForApplication(access.EntityId, applicationId);
            await _entityAccessRepository.Update(_mapper.Map<EntityAccess>(access));
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<EntityAccessDto> GetById(int id, int applicationId)
        {
            return _mapper.Map<EntityAccessDto>(await _entityAccessRepository.GetByIdForApplication(id, applicationId));
        }

        public List<EntityAccessDto> List(int applicationId)
        {
            return _mapper.Map<List<EntityAccessDto>>(_entityAccessRepository.List(applicationId));
        }
        public List<EntityAccessDto> GetEntityAccesses(int entityId)
        {
            return _mapper.Map<List<EntityAccessDto>>(_entityAccessRepository.GetEntityAccesses(entityId));
        }
    }
}
