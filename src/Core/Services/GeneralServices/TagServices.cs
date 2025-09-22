using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.GeneralDtos;
using Domains.Entities.General;
using Infrastructure.GeneralRepository;

namespace Services.GeneralServices
{
    public class TagServices : ITagServices
    {
        // fields 
        private readonly ITagRepository _tagRepository;

        // constructor
        public TagServices(ITagRepository tagRepository)
        {
            _tagRepository = tagRepository;
        }

        // methods
        public async Task<TagDto> Create(TagDto tag)
        {
            tag.IsActive = true;
            var result = await _tagRepository.Create(Mapper(tag));
            return Mapper(result);
        }

        public async Task Delete(int id)
        {
            await _tagRepository.Delete(id);
        }

        public List<TagDto> List(int applicationId)
        {
            return Mapper(_tagRepository.List(applicationId));
        }

        public List<TagDto> FindTagsByTypeId(int applicationId, int typeId)
        {
            return Mapper(_tagRepository.FindTagsByTypeId(applicationId, typeId));
        }

        public async Task<TagDto> GetById(int id)
        {
            return Mapper(await _tagRepository.GetById(id));
        }

        // mapper
        private Tag Mapper(TagDto tag)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<TagDto, Tag>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<TagDto, Tag>(tag);
        }
        private TagDto Mapper(Tag tag)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Tag, TagDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<Tag, TagDto>(tag);
        }
        private List<TagDto> Mapper(List<Tag> tag)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Tag, TagDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<Tag>, List<TagDto>>(tag);
        }
    }
}