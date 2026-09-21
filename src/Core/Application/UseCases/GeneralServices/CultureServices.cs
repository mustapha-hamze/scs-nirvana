using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.General;
using Domains.Entities.General;
using Application.GeneralRepository;
using Application.UnitOfWork;

namespace Application.UseCases.GeneralServices
{
    public class CultureServices : ICultureServices
    {
        // fields
        private readonly ICultureRepository _cultureRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // constructor
        public CultureServices(ICultureRepository cultureRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _cultureRepository = cultureRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task<CultureDto> Create(CultureDto culture, CancellationToken cancellationToken = default)
        {
            culture.IsActive = true;
            var created = await _cultureRepository.Create(_mapper.Map<Culture>(culture));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<CultureDto>(created);
        }

        public async Task Delete(int id, CancellationToken cancellationToken = default)
        {
            await _cultureRepository.Delete(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<CultureDto>> List(CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<CultureDto>>(await _cultureRepository.List(cancellationToken));
        }

        public async Task<CultureDto> GetById(int id, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<CultureDto>(await _cultureRepository.GetById(id, cancellationToken));
        }
    }
}