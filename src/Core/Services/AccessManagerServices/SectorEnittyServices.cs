using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.AccessManagement;
using Infrastructure.AccessManagerRepository;
using Infrastructure.Dto.AccessManagerDtos;

namespace Services.AccessManagerServices
{
    public class SectorEntityServices : ISectorEntityServices
    {
        // fields
        private readonly ISectorEntityRepository _sectorEntityRepository;

        // constructor
        public SectorEntityServices(ISectorEntityRepository sectorEntityRepository)
        {
            _sectorEntityRepository = sectorEntityRepository;
        }

        // methods
        public async Task Create(SectorEntityDto sectorEntity)
        {
            await _sectorEntityRepository.Create(Mapper(sectorEntity));
        }

        public async Task Update(SectorEntityDto sectorEntity)
        {
            await _sectorEntityRepository.Update(Mapper(sectorEntity));
        }

        public List<SectorEntityDto> GetSectorEntities(int sectorId)
        {
            return Mapper(_sectorEntityRepository.GetSectorEntities(sectorId));
        }

        public List<SectorEntityDto> GetAllEntities()
        {
            return Mapper(_sectorEntityRepository.GetAllEntities());
        }

        public async Task<SectorEntityDto> GetById(int id)
        {
            return Mapper(await _sectorEntityRepository.GetById(id));
        }

        // mapper
        private SectorEntity Mapper(SectorEntityDto sectorEntity)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SectorEntityDto, SectorEntity>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SectorEntityDto, SectorEntity>(sectorEntity);
        }

        private SectorEntityDto Mapper(SectorEntity sectorEntity)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SectorEntity, SectorEntityDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SectorEntity, SectorEntityDto>(sectorEntity);
        }

        private List<SectorEntityDto> Mapper(List<SectorEntity> sectorEntities)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SectorEntity, SectorEntityDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<SectorEntity>, List<SectorEntityDto>>(sectorEntities);
        }
    }
}