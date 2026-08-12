using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.AccessManagement;
using Application.AccessManagerRepository;
using Infrastructure.Dto.AccessManagerDtos;

namespace Services.AccessManagerServices
{
    public class EntityAccessServices : IEntityAccessServices
    {
        private readonly IEntityAccessRepository _entityAccessRepository;

        public EntityAccessServices(IEntityAccessRepository entityAccessRepository)
        {
            _entityAccessRepository = entityAccessRepository;
        }

        public async Task Create(EntityAccessDto access)
        {
            await _entityAccessRepository.Create(Mapper(access));
        }

        public async Task Update(EntityAccessDto access)
        {
            await _entityAccessRepository.Update(Mapper(access));
        }

        public async Task<EntityAccessDto> GetById(int id)
        {
            return Mapper(await _entityAccessRepository.GetById(id));
        }

        public List<EntityAccessDto> List(int applicationId)
        {
            return Mapper(_entityAccessRepository.List(applicationId));
        }
        public List<EntityAccessDto> GetEntityAccesses(int entityId)
        {
            return Mapper(_entityAccessRepository.GetEntityAccesses(entityId));
        }

        // mapper
        private EntityAccess Mapper(EntityAccessDto entityAccess)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<EntityAccessDto, EntityAccess>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<EntityAccessDto, EntityAccess>(entityAccess);
        }
        private EntityAccessDto Mapper(EntityAccess entityAccess)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<EntityAccess, EntityAccessDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<EntityAccess, EntityAccessDto>(entityAccess);
        }

        private List<EntityAccessDto> Mapper(List<EntityAccess> entityAccesses)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<EntityAccess, EntityAccessDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<EntityAccess>, List<EntityAccessDto>>(entityAccesses);
        }
    }
}