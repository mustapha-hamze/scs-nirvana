using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.GeneralDtos;
using Domains.Entities.General;
using Infrastructure.GeneralRepository;

namespace Services.GeneralServices
{
    public class CultureServices : ICultureServices
    {
        // fields
        private readonly ICultureRepository _cultureRepository;

        // constructor
        public CultureServices(ICultureRepository cultureRepository)
        {
            _cultureRepository = cultureRepository;
        }

        // methods
        public async Task<CultureDto> Create(CultureDto culture)
        {
            culture.IsActive = true;
            return Mapper(await _cultureRepository.Create(Mapper(culture)));
        }

        public async Task Delete(int id)
        {
            await _cultureRepository.Delete(id);
        }

        public List<CultureDto> List()
        {
            return Mapper(_cultureRepository.List());
        }

        public async Task<CultureDto> GetById(int id)
        {
            return Mapper(await _cultureRepository.GetById(id));
        }

        // mapper
        private Culture Mapper(CultureDto culture)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<CultureDto, Culture>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<CultureDto, Culture>(culture);
        }
        private CultureDto Mapper(Culture culture)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Culture, CultureDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<Culture, CultureDto>(culture);
        }
        private List<CultureDto> Mapper(List<Culture> culture)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Culture, CultureDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<Culture>, List<CultureDto>>(culture);
        }
    }
}