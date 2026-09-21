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
        public async Task<CultureDto> Create(CultureDto culture)
        {
            culture.IsActive = true;
            var created = await _cultureRepository.Create(_mapper.Map<Culture>(culture));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<CultureDto>(created);
        }

        public async Task Delete(int id)
        {
            await _cultureRepository.Delete(id);
            await _unitOfWork.SaveChangesAsync();
        }

        public List<CultureDto> List()
        {
            return _mapper.Map<List<CultureDto>>(_cultureRepository.List());
        }

        public async Task<CultureDto> GetById(int id)
        {
            return _mapper.Map<CultureDto>(await _cultureRepository.GetById(id));
        }
    }
}
