using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.AccessManagement;
using Application.AccessManagerRepository;
using Application.Contracts.AccessManagement;

namespace Services.AccessManagerServices
{
    public class SectorServices : ISectorServices
    {
        // fields
        private readonly ISectorRepository _sectorRepository;
        private readonly IMapper _mapper;

        // constructor
        public SectorServices(ISectorRepository sectorRepository, IMapper mapper)
        {
            _sectorRepository = sectorRepository;
            _mapper = mapper;
        }

        // methods
        public async Task Create(SectorDto sector)
        {
            await _sectorRepository.Create(_mapper.Map<Sector>(sector));
        }

        public async Task Update(SectorDto sector)
        {
            await _sectorRepository.Update(_mapper.Map<Sector>(sector));
        }

        public async Task<SectorDto> GetById(int id)
        {
            return _mapper.Map<SectorDto>(await _sectorRepository.GetById(id));
        }

        public async Task Delete(int id)
        {
            await _sectorRepository.Delete(id);
        }

        public List<SectorDto> GetAllSector(int applicationId)
        {
            return _mapper.Map<List<SectorDto>>(_sectorRepository.GetAllSector(applicationId));
        }

        public List<SectorDto> GetAllSector()
        {
            return _mapper.Map<List<SectorDto>>(_sectorRepository.GetAllSector());
        }
    }
}
