using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.General;
using Domains.Entities.General;

namespace Services.GeneralServices
{
    public class TagServices : ITagServices
    {
        // fields
        private readonly global::Application.GeneralRepository.ITagRepository _tagRepository;
        private readonly IMapper _mapper;

        // constructor
        public TagServices(global::Application.GeneralRepository.ITagRepository tagRepository, IMapper mapper)
        {
            _tagRepository = tagRepository;
            _mapper = mapper;
        }

        // methods
        public async Task<TagDto> Create(TagDto tag)
        {
            tag.IsActive = true;
            var result = await _tagRepository.Create(_mapper.Map<Tag>(tag));
            return _mapper.Map<TagDto>(result);
        }

        public async Task Delete(int id)
        {
            await _tagRepository.Delete(id);
        }

        public List<TagDto> List(int applicationId)
        {
            return _mapper.Map<List<TagDto>>(_tagRepository.List(applicationId));
        }

        public List<TagDto> FindTagsByTypeId(int applicationId, int typeId)
        {
            return _mapper.Map<List<TagDto>>(_tagRepository.FindTagsByTypeId(applicationId, typeId));
        }

        public async Task<TagDto> GetById(int id)
        {
            return _mapper.Map<TagDto>(await _tagRepository.GetById(id));
        }
    }
}
