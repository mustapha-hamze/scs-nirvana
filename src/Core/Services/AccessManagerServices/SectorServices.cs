using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.AccessManagement;
using Infrastructure.AccessManagerRepository;
using Infrastructure.Dto.AccessManagerDtos;

namespace Services.AccessManagerServices
{
    public class SectorServices : ISectorServices
    {
        // fields
        private readonly ISectorRepository _sectorRepository;

        // constructor
        public SectorServices(ISectorRepository sectorRepository)
        {
            _sectorRepository = sectorRepository;
        }

        // methods
        public async Task Create(SectorDto sector)
        {
            await _sectorRepository.Create(Mapper(sector));
        }

        public async Task Update(SectorDto sector)
        {
            await _sectorRepository.Update(Mapper(sector));
        }

        public async Task<SectorDto> GetById(int id)
        {
            return Mapper(await _sectorRepository.GetById(id));
        }

        public async Task Delete(int id)
        {
            await _sectorRepository.Delete(id);
        }

        public List<SectorDto> GetAllSector(int applicationId)
        {
            return Mapper(_sectorRepository.GetAllSector(applicationId));
        }

        public List<SectorDto> GetAllSector()
        {
            return Mapper(_sectorRepository.GetAllSector());
        }

        // mapper
        private Sector Mapper(SectorDto sector)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SectorDto, Sector>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SectorDto, Sector>(sector);
        }
        private SectorDto Mapper(Sector sector)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Sector, SectorDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<Sector, SectorDto>(sector);
        }

        private List<SectorDto> Mapper(List<Sector> sectors)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Sector, SectorDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<Sector>, List<SectorDto>>(sectors);
        }
    }
}