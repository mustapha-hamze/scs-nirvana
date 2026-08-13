using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.AccessManagement;
using Application.AccessManagerRepository;
using Application.Contracts.AccessManagement;

namespace Services.AccessManagerServices
{
    public class EntityAccessServices : IEntityAccessServices
    {
        private readonly IEntityAccessRepository _entityAccessRepository;
        private readonly IMapper _mapper;

        public EntityAccessServices(IEntityAccessRepository entityAccessRepository, IMapper mapper)
        {
            _entityAccessRepository = entityAccessRepository;
            _mapper = mapper;
        }

        public async Task Create(EntityAccessDto access)
        {
            await _entityAccessRepository.Create(_mapper.Map<EntityAccess>(access));
        }

        public async Task Update(EntityAccessDto access)
        {
            await _entityAccessRepository.Update(_mapper.Map<EntityAccess>(access));
        }

        public async Task<EntityAccessDto> GetById(int id)
        {
            return _mapper.Map<EntityAccessDto>(await _entityAccessRepository.GetById(id));
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
