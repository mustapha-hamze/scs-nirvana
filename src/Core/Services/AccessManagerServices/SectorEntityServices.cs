using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ISectorRepository _sectorRepository;
        private readonly IMapper _mapper;

        // constructor
        public SectorEntityServices(ISectorEntityRepository sectorEntityRepository, ISectorRepository sectorRepository, IMapper mapper)
        {
            _sectorEntityRepository = sectorEntityRepository;
            _sectorRepository = sectorRepository;
            _mapper = mapper;
        }

        // methods
        public async Task Create(SectorEntityDto sectorEntity, int applicationId)
        {
            // The parent Sector must belong to this application before an entity can be filed under it.
            await _sectorRepository.GetByIdForApplication(sectorEntity.SectorId, applicationId);
            await _sectorEntityRepository.Create(_mapper.Map<SectorEntity>(sectorEntity));
        }

        public async Task Update(SectorEntityDto sectorEntity, int applicationId)
        {
            await _sectorEntityRepository.GetByIdForApplication(sectorEntity.Id, applicationId);
            await _sectorRepository.GetByIdForApplication(sectorEntity.SectorId, applicationId);
            await _sectorEntityRepository.Update(_mapper.Map<SectorEntity>(sectorEntity));
        }

        public List<SectorEntityDto> GetSectorEntities(int sectorId, int applicationId)
        {
            if (!_sectorRepository.GetAllSector(applicationId).Any(s => s.Id == sectorId))
                throw new KeyNotFoundException();

            return _mapper.Map<List<SectorEntityDto>>(_sectorEntityRepository.GetSectorEntities(sectorId));
        }

        public List<SectorEntityDto> GetSectorEntities(int sectorId)
        {
            return _mapper.Map<List<SectorEntityDto>>(_sectorEntityRepository.GetSectorEntities(sectorId));
        }

        public List<SectorEntityDto> GetAllEntities()
        {
            return _mapper.Map<List<SectorEntityDto>>(_sectorEntityRepository.GetAllEntities());
        }

        public async Task<SectorEntityDto> GetById(int id, int applicationId)
        {
            return _mapper.Map<SectorEntityDto>(await _sectorEntityRepository.GetByIdForApplication(id, applicationId));
        }
    }
}
