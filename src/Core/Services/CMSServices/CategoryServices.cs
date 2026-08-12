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

        // constructor
        public CategoryServices(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        // methods
        public async Task<CategoryDto> Create(CategoryDto category)
        {
            category.IsActive = true;
            return Mapper(await _categoryRepository.Create(Mapper(category)));
        }

        public async Task Delete(int id)
        {
            await _categoryRepository.Delete(id);
        }

        public List<CategoryDto> List(int applicationId)
        {
            return Mapper(_categoryRepository.List(applicationId));
        }

        public async Task<CategoryDto> GetById(int id)
        {
            return Mapper(await _categoryRepository.GetById(id));
        }

        public List<CategoryDto> GetAllFullPath(int applicationId)
        {
            return Mapper(_categoryRepository.GetAllFullPath(applicationId));
        }

        // mapper
        private Category Mapper(CategoryDto category)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<CategoryDto, Category>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<CategoryDto, Category>(category);
        }
        private CategoryDto Mapper(Category category)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Category, CategoryDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<Category, CategoryDto>(category);
        }
        private List<CategoryDto> Mapper(List<Category> category)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Category, CategoryDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<Category>, List<CategoryDto>>(category);
        }
    }
}