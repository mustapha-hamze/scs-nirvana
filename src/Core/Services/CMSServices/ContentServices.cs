using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.CMSDtos;
using Domains.Entities.ContentManagement;
using Infrastructure;
using Infrastructure.CMSRepository;
using Infrastructure.Repository;

namespace Services.CMSServices
{
    public class ContentServices : IContentServices
    {
        // fileds
        private readonly IContentRepository _contentRepository;
        private readonly IRepository<ContentSection> _contentSectionRepository;
        private readonly IRepository<SectionElement> _sectionElementRepository;
        private readonly IRepository<ContentMetadata> _contnentMetadataRepository;
        private readonly IRepository<ContentImage> _contentImageRepository;

        // constructor
        public ContentServices(IContentRepository contentRepository, IRepository<ContentSection> contentSectionRepository,
        IRepository<SectionElement> sectionElementRepository, IRepository<ContentMetadata> contnentMetadataRepository, IRepository<ContentImage> contentImageRepository)
        {
            _contentRepository = contentRepository;
            _contentSectionRepository = contentSectionRepository;
            _sectionElementRepository = sectionElementRepository;
            _contnentMetadataRepository = contnentMetadataRepository;
            _contentImageRepository = contentImageRepository;
        }

        // methods
        public async Task<ContentDto> Create(ContentDto content)
        {
            return Mapper(await _contentRepository.Create(Mapper(content)));
        }

        public async Task Delete(int id)
        {
            await _contentRepository.Delete(id);
        }

        public List<Content> GetContentByIdFull(int id)
        {
            return _contentRepository.GetContentByIdFull(id);
        }

        public List<Content> GetContentByTypeId(int typeId)
        {
            return _contentRepository.GetContentByTypeId(typeId);
        }

        public BlogIndexDto GetContentByTypeId(int typeId, int pageIndex = 1)
        {
            return _contentRepository.GetContentByTypeId(typeId, pageIndex);
        }

        public BlogIndexDto GetContentByCategoryId(int categoryId, int pageIndex = 1, int pageSize = 40)
        {
            return _contentRepository.GetContentByCategoryId(categoryId, pageIndex, pageSize);
        }

        public BlogIndexDto GetContentByCategoryIdByDate(int categoryId, DateTime startDate, DateTime endDate, int pageIndex)
        {
            return _contentRepository.GetContentByCategoryIdByDate(categoryId, startDate, endDate, pageIndex);
        }

        public async Task ChangeContentActiveMode(int id, bool mode)
        {
            var content = await _contentRepository.GetById(id);
            content.IsActive = mode;
            await _contentRepository.Update(content);
        }

        public async Task<ContentDto> Update(ContentDto content)
        {
            return Mapper(await _contentRepository.Update(Mapper(content)));
        }

        public async Task<ContentDto> GetById(int id)
        {
            return Mapper(await _contentRepository.GetById(id));
        }

        public List<ContentDto> List(int applicationId)
        {
            return Mapper(_contentRepository.List(applicationId));
        }

        public List<ContentDto> List(int applicationId, int pageIndex)
        {
            return Mapper(_contentRepository.List(applicationId, pageIndex));
        }

        public List<ContentDto> OurBlogBoxList(int applicationId)
        {
            return Mapper(_contentRepository.OurBlogBoxList(applicationId));
        }

        public async Task<SectionDto> CreateSection(SectionDto section)
        {
            return Mapper(await _contentSectionRepository.Create(Mapper(section)));
        }

        public async Task<SectionElementDto> CreateSectionElement(SectionElementDto sectionElement)
        {
            return Mapper(await _sectionElementRepository.Create(Mapper(sectionElement)));
        }

        public async Task UpdateSectionElement(SectionElementDto sectionElement)
        {
            var element = await _sectionElementRepository.GetById(sectionElement.Id);

            element.EditorText = sectionElement.EditorText;
            element.FileNameText = sectionElement.FileNameText;
            element.GalleryImages = sectionElement.GalleryImages;
            element.TinyText = sectionElement.TinyText;

            element.UpdatedDT = DateTime.Now;

            await _sectionElementRepository.Update(element);
        }

        public List<SectionDto> GetSections(int contentId)
        {
            var sections = _contentRepository.GetContentSections(contentId);

            var _sections = new List<SectionDto>();
            foreach (var item in sections)
            {
                var __section = new SectionDto();
                __section = Mapper(item);
                var element = Mapper(_contentRepository.GetSectionElements(item.Id));
                __section.SectionElements = element;
                _sections.Add(__section);
            }

            return _sections;
        }

        public int ContentCount(int applicationId)
        {
            return _contentRepository.ContentCount(applicationId);
        }

        public async Task CreateContentCategories(string data, string entity, int contentId)
        {
            await _contentRepository.CreateContentCategories(data, entity, contentId);
        }

        public async Task CreateContentTags(string data, string entity, int contentId)
        {
            await _contentRepository.CreateContentTags(data, entity, contentId);
        }

        public async Task CreateContentCultures(string data, string entity, int contentId)
        {
            await _contentRepository.CreateContentCultures(data, entity, contentId);
        }

        public ContentMetadataDto GetContentMetadata(int contentId)
        {
            return Mapper(_contentRepository.GetContentMetadata(contentId));
        }

        public async Task<ContentMetadataDto> CreateContentMetadata(ContentMetadataDto contentMetadata)
        {
            contentMetadata.IsActive = true;
            return Mapper(await _contnentMetadataRepository.Create(Mapper(contentMetadata)));
        }

        public async Task<ContentMetadataDto> UpdateContentMetadata(ContentMetadataDto contentMetadata)
        {
            return Mapper(await _contnentMetadataRepository.Update(Mapper(contentMetadata)));
        }

        public async Task CreateContentImage(ContentImageDto contentImage)
        {
            await _contentImageRepository.Create(Mapper(contentImage));
        }

        public async Task DeleteAllContentImages(int contentId)
        {
            await _contentRepository.DeleteAllContentImages(contentId);
        }

        public List<ContentImageDto> GetAllContentImages(int contentId)
        {
            return Mapper(_contentRepository.GetAllContentImages(contentId));
        }

        public async Task DeleteSection(int sectionId)
        {
            await _contentSectionRepository.Delete(sectionId);
        }

        public async Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId)
        {
            return await _contentRepository.GetContentsInCategory(categoryId, applicationId);
        }

        public List<Content> GetContentInCategoryAsBox(int categoryId)
        {
            return _contentRepository.GetContentInCategoryAsBox(categoryId);
        }

        // mapoper
        private Content Mapper(ContentDto content)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ContentDto, Content>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ContentDto, Content>(content);
        }
        private ContentDto Mapper(Content content)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Content, ContentDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<Content, ContentDto>(content);
        }
        private List<ContentDto> Mapper(List<Content> content)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Content, ContentDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<Content>, List<ContentDto>>(content);
        }
        private ContentSection Mapper(SectionDto section)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SectionDto, ContentSection>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SectionDto, ContentSection>(section);
        }
        private SectionDto Mapper(ContentSection section)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ContentSection, SectionDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ContentSection, SectionDto>(section);
        }
        private SectionElement Mapper(SectionElementDto sectionElement)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SectionElementDto, SectionElement>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SectionElementDto, SectionElement>(sectionElement);
        }
        private SectionElementDto Mapper(SectionElement sectionElement)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SectionElement, SectionElementDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<SectionElement, SectionElementDto>(sectionElement);
        }
        private List<SectionElementDto> Mapper(List<SectionElement> element)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<SectionElement, SectionElementDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<SectionElement>, List<SectionElementDto>>(element);
        }
        private ContentMetadataDto Mapper(ContentMetadata contentMetadata)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ContentMetadata, ContentMetadataDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ContentMetadata, ContentMetadataDto>(contentMetadata);
        }
        private ContentMetadata Mapper(ContentMetadataDto contentMetadata)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ContentMetadataDto, ContentMetadata>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ContentMetadataDto, ContentMetadata>(contentMetadata);
        }
        private ContentImage Mapper(ContentImageDto contentImage)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ContentImageDto, ContentImage>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<ContentImageDto, ContentImage>(contentImage);
        }
        private List<ContentImageDto> Mapper(List<ContentImage> contentImage)
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<ContentImage, ContentImageDto>()
            );

            IMapper mapper = config.CreateMapper();
            return mapper.Map<List<ContentImage>, List<ContentImageDto>>(contentImage);
        }
    }
}