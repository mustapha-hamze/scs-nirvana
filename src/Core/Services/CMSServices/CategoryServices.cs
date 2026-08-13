using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.CMSDtos;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;

namespace Services.CMSServices
{
    public class CategoryServices : ICategoryServices
    {
        // fields
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;

        // constructor
        public CategoryServices(ICategoryRepository categoryRepository, IMapper mapper)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
        }

        // methods
        public async Task<CategoryDto> Create(CategoryDto category)
        {
            category.IsActive = true;
            return _mapper.Map<CategoryDto>(await _categoryRepository.Create(_mapper.Map<Category>(category)));
        }

        public async Task Delete(int id)
        {
            await _categoryRepository.Delete(id);
        }

        public List<CategoryDto> List(int applicationId)
        {
            return _mapper.Map<List<CategoryDto>>(_categoryRepository.List(applicationId));
        }

        public async Task<CategoryDto> GetById(int id)
        {
            return _mapper.Map<CategoryDto>(await _categoryRepository.GetById(id));
        }

        public List<CategoryDto> GetAllFullPath(int applicationId)
        {
            return _mapper.Map<List<CategoryDto>>(_categoryRepository.GetAllFullPath(applicationId));
        }
    }
}
