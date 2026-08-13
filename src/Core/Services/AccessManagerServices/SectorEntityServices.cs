using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.AccessManagement;
using Application.AccessManagerRepository;
using Application.Contracts.AccessManagement;

namespace Services.AccessManagerServices
{
    public class SectorEntityServices : ISectorEntityServices
    {
        // fields
        private readonly ISectorEntityRepository _sectorEntityRepository;
        private readonly IMapper _mapper;

        // constructor
        public SectorEntityServices(ISectorEntityRepository sectorEntityRepository, IMapper mapper)
        {
            _sectorEntityRepository = sectorEntityRepository;
            _mapper = mapper;
        }

        // methods
        public async Task Create(SectorEntityDto sectorEntity)
        {
            await _sectorEntityRepository.Create(_mapper.Map<SectorEntity>(sectorEntity));
        }

        public async Task Update(SectorEntityDto sectorEntity)
        {
            await _sectorEntityRepository.Update(_mapper.Map<SectorEntity>(sectorEntity));
        }

        public List<SectorEntityDto> GetSectorEntities(int sectorId)
        {
            return _mapper.Map<List<SectorEntityDto>>(_sectorEntityRepository.GetSectorEntities(sectorId));
        }

        public List<SectorEntityDto> GetAllEntities()
        {
            return _mapper.Map<List<SectorEntityDto>>(_sectorEntityRepository.GetAllEntities());
        }

        public async Task<SectorEntityDto> GetById(int id)
        {
            return _mapper.Map<SectorEntityDto>(await _sectorEntityRepository.GetById(id));
        }
    }
}
