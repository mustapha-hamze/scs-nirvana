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
        public async Task<CategoryDto> Create(CategoryDto category, int applicationId)
        {
            category.IsActive = true;
            // Never trust ApplicationId from the DTO - server-pin it.
            category.ApplicationId = applicationId;
            var created = await _categoryRepository.Create(_mapper.Map<Category>(category));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<CategoryDto>(created);
        }

        public async Task Delete(int id, int applicationId)
        {
            await _categoryRepository.GetByIdForApplication(id, applicationId);
            await _categoryRepository.Delete(id);
            await _unitOfWork.SaveChangesAsync();
        }

        public List<CategoryDto> List(int applicationId)
        {
            return _mapper.Map<List<CategoryDto>>(_categoryRepository.List(applicationId));
        }

        public async Task<CategoryDto> GetById(int id, int applicationId)
        {
            return _mapper.Map<CategoryDto>(await _categoryRepository.GetByIdForApplication(id, applicationId));
        }

        public List<CategoryDto> GetAllFullPath(int applicationId)
        {
            return _mapper.Map<List<CategoryDto>>(_categoryRepository.GetAllFullPath(applicationId));
        }
    }
}
