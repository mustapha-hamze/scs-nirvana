using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.General;
using Domains.Entities.General;
using Application.GeneralRepository;

namespace Services.GeneralServices
{
    public class CultureServices : ICultureServices
    {
        // fields
        private readonly ICultureRepository _cultureRepository;
        private readonly IMapper _mapper;

        // constructor
        public CultureServices(ICultureRepository cultureRepository, IMapper mapper)
        {
            _cultureRepository = cultureRepository;
            _mapper = mapper;
        }

        // methods
        public async Task<CultureDto> Create(CultureDto culture)
        {
            culture.IsActive = true;
            return _mapper.Map<CultureDto>(await _cultureRepository.Create(_mapper.Map<Culture>(culture)));
        }

        public async Task Delete(int id)
        {
            await _cultureRepository.Delete(id);
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
