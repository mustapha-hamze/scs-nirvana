using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.CMS;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.UnitOfWork;

namespace Application.UseCases.CMSServices
{
    public class CategoryServices : ICategoryServices
    {
        // fields
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // constructor
        public CategoryServices(ICategoryRepository categoryRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task<CategoryDto> Create(CategoryDto category, int applicationId, CancellationToken cancellationToken = default)
        {
            category.IsActive = true;
            // Never trust ApplicationId from the DTO - server-pin it.
            category.ApplicationId = applicationId;
            var created = await _categoryRepository.Create(_mapper.Map<Category>(category));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<CategoryDto>(created);
        }

        public async Task Delete(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            await _categoryRepository.GetByIdForApplication(id, applicationId, cancellationToken);
            await _categoryRepository.Delete(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<CategoryDto>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<CategoryDto>>(await _categoryRepository.List(applicationId, cancellationToken));
        }

        public async Task<CategoryDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<CategoryDto>(await _categoryRepository.GetByIdForApplication(id, applicationId, cancellationToken));
        }

        public async Task<List<CategoryDto>> GetAllFullPath(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<CategoryDto>>(await _categoryRepository.GetAllFullPath(applicationId, cancellationToken));
        }
    }
}