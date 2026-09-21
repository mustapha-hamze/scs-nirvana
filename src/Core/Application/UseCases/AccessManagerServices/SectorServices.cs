using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Domains.Entities.AccessManagement;
using Application.AccessManagerRepository;
using Application.Contracts.AccessManagement;
using Application.UnitOfWork;

namespace Application.UseCases.AccessManagerServices
{
    public class SectorServices : ISectorServices
    {
        // fields
        private readonly ISectorRepository _sectorRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // constructor
        public SectorServices(ISectorRepository sectorRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _sectorRepository = sectorRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task Create(SectorDto sector, CancellationToken cancellationToken = default)
        {
            await _sectorRepository.Create(_mapper.Map<Sector>(sector));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task Update(SectorDto sector, int applicationId, CancellationToken cancellationToken = default)
        {
            // Confirms the sector being edited already belongs to this application before
            // touching it, and re-pins ApplicationId server-side so a caller can't use this
            // endpoint to move a sector into a different application.
            await _sectorRepository.GetByIdForApplication(sector.Id, applicationId, cancellationToken);
            sector.ApplicationId = applicationId;
            await _sectorRepository.Update(_mapper.Map<Sector>(sector));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<SectorDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<SectorDto>(await _sectorRepository.GetByIdForApplication(id, applicationId, cancellationToken));
        }

        public async Task Delete(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            await _sectorRepository.GetByIdForApplication(id, applicationId, cancellationToken);
            await _sectorRepository.Delete(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<SectorDto>> GetAllSector(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<SectorDto>>(await _sectorRepository.GetAllSector(applicationId, cancellationToken));
        }

        public async Task<List<SectorDto>> GetAllSector(CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<SectorDto>>(await _sectorRepository.GetAllSector(cancellationToken));
        }
    }
}