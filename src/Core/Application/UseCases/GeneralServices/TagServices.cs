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
        public async Task<TagDto> Create(TagDto tag, int applicationId, CancellationToken cancellationToken = default)
        {
            tag.IsActive = true;
            // Never trust ApplicationId from the DTO - server-pin it.
            tag.ApplicationId = applicationId;
            var result = await _tagRepository.Create(_mapper.Map<Tag>(tag));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<TagDto>(result);
        }

        public async Task Delete(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            await _tagRepository.GetByIdForApplication(id, applicationId, cancellationToken);
            await _tagRepository.Delete(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<TagDto>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<TagDto>>(await _tagRepository.List(applicationId, cancellationToken));
        }

        public async Task<List<TagDto>> FindTagsByTypeId(int applicationId, int typeId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<TagDto>>(await _tagRepository.FindTagsByTypeId(applicationId, typeId, cancellationToken));
        }

        public async Task<TagDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<TagDto>(await _tagRepository.GetByIdForApplication(id, applicationId, cancellationToken));
        }
    }
}