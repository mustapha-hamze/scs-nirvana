using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.General;
using Domains.Entities.General;
using Application.UnitOfWork;

namespace Application.UseCases.GeneralServices
{
    public class TagServices : ITagServices
    {
        // fields
        private readonly global::Application.GeneralRepository.ITagRepository _tagRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // constructor
        public TagServices(global::Application.GeneralRepository.ITagRepository tagRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _tagRepository = tagRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task<TagDto> Create(TagDto tag, int applicationId)
        {
            tag.IsActive = true;
            // Never trust ApplicationId from the DTO - server-pin it.
            tag.ApplicationId = applicationId;
            var result = await _tagRepository.Create(_mapper.Map<Tag>(tag));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<TagDto>(result);
        }

        public async Task Delete(int id, int applicationId)
        {
            await _tagRepository.GetByIdForApplication(id, applicationId);
            await _tagRepository.Delete(id);
            await _unitOfWork.SaveChangesAsync();
        }

        public List<TagDto> List(int applicationId)
        {
            return _mapper.Map<List<TagDto>>(_tagRepository.List(applicationId));
        }

        public List<TagDto> FindTagsByTypeId(int applicationId, int typeId)
        {
            return _mapper.Map<List<TagDto>>(_tagRepository.FindTagsByTypeId(applicationId, typeId));
        }

        public async Task<TagDto> GetById(int id, int applicationId)
        {
            return _mapper.Map<TagDto>(await _tagRepository.GetByIdForApplication(id, applicationId));
        }
    }
}
