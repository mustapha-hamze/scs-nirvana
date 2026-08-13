using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Infrastructure.Dto.CMSDtos;
using Domains.Entities.ContentManagement;
using Application.Repository;
using Infrastructure.CMSRepository;

namespace Services.CMSServices
{
    public class ContentServices : IContentServices
    {
        // fields
        private readonly IContentRepository _contentRepository;
        private readonly IRepository<ContentSection> _contentSectionRepository;
        private readonly IRepository<SectionElement> _sectionElementRepository;
        private readonly IRepository<ContentMetadata> _contentMetadataRepository;
        private readonly IRepository<ContentImage> _contentImageRepository;
        private readonly IMapper _mapper;

        // constructor
        public ContentServices(IContentRepository contentRepository, IRepository<ContentSection> contentSectionRepository,
        IRepository<SectionElement> sectionElementRepository, IRepository<ContentMetadata> contentMetadataRepository, IRepository<ContentImage> contentImageRepository,
        IMapper mapper)
        {
            _contentRepository = contentRepository;
            _contentSectionRepository = contentSectionRepository;
            _sectionElementRepository = sectionElementRepository;
            _contentMetadataRepository = contentMetadataRepository;
            _contentImageRepository = contentImageRepository;
            _mapper = mapper;
        }

        // methods
        public async Task<ContentDto> Create(ContentDto content)
        {
            return _mapper.Map<ContentDto>(await _contentRepository.Create(_mapper.Map<Content>(content)));
        }

        public async Task Delete(int id)
        {
            await _contentRepository.Delete(id);
        }

        public List<ContentApiDto> GetContentByIdFull(int id)
        {
            return _contentRepository.GetContentByIdFull(id);
        }

        public List<ContentApiDto> GetContentByTypeId(int typeId)
        {
            return _contentRepository.GetContentByTypeId(typeId);
        }

        public BlogIndexApiDto GetContentByTypeId(int typeId, int pageIndex = 1)
        {
            return _contentRepository.GetContentByTypeId(typeId, pageIndex);
        }

        public BlogIndexApiDto GetContentByCategoryId(int categoryId, int pageIndex = 1, int pageSize = 40)
        {
            return _contentRepository.GetContentByCategoryId(categoryId, pageIndex, pageSize);
        }

        public BlogIndexApiDto GetContentByCategoryIdByDate(int categoryId, DateTime startDate, DateTime endDate, int pageIndex)
        {
            return _contentRepository.GetContentByCategoryIdByDate(categoryId, startDate, endDate, pageIndex);
        }

        public async Task ChangeContentActiveMode(int id, bool mode)
        {
            var content = await _contentRepository.GetById(id);
            content.IsActive = mode;
            await _contentRepository.Update(content);
        }

        public async Task UpdateTranslate(int contentId, string translatedContent)
        {
            var content = await _contentRepository.GetById(contentId);
            content.FarsiContent = translatedContent;
            await _contentRepository.Update(content);
        }

        public async Task<ContentDto> Update(ContentDto content)
        {
            return _mapper.Map<ContentDto>(await _contentRepository.Update(_mapper.Map<Content>(content)));
        }

        // See IContentServices.Update(Content) — legacy/internal path for the Farsi-translation
        // flow only.
        public async Task<Content> Update(Content content)
        {
            return await _contentRepository.Update(content);
        }

        public async Task<ContentDto> GetById(int id)
        {
            return _mapper.Map<ContentDto>(await _contentRepository.GetById(id));
        }

        public List<ContentDto> List(int applicationId)
        {
            return _mapper.Map<List<ContentDto>>(_contentRepository.List(applicationId));
        }

        public List<ContentDto> List(int applicationId, int pageIndex)
        {
            return _mapper.Map<List<ContentDto>>(_contentRepository.List(applicationId, pageIndex));
        }

        public List<ContentDto> OurBlogBoxList(int applicationId)
        {
            return _mapper.Map<List<ContentDto>>(_contentRepository.OurBlogBoxList(applicationId));
        }

        public async Task<SectionDto> CreateSection(SectionDto section)
        {
            return _mapper.Map<SectionDto>(await _contentSectionRepository.Create(_mapper.Map<ContentSection>(section)));
        }

        public async Task<SectionElementDto> CreateSectionElement(SectionElementDto sectionElement)
        {
            return _mapper.Map<SectionElementDto>(await _sectionElementRepository.Create(_mapper.Map<SectionElement>(sectionElement)));
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

            var elementsBySectionId = _contentRepository.GetSectionElements(sections.Select(s => s.Id).ToList())
                .GroupBy(e => e.SectionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var _sections = new List<SectionDto>();
            foreach (var item in sections)
            {
                var __section = new SectionDto();
                __section = _mapper.Map<SectionDto>(item);
                var elements = elementsBySectionId.TryGetValue(item.Id, out var sectionElements) ? sectionElements : new List<SectionElement>();
                __section.SectionElements = _mapper.Map<List<SectionElementDto>>(elements);
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
            return _mapper.Map<ContentMetadataDto>(_contentRepository.GetContentMetadata(contentId));
        }

        public async Task<ContentMetadataDto> CreateContentMetadata(ContentMetadataDto contentMetadata)
        {
            contentMetadata.IsActive = true;
            return _mapper.Map<ContentMetadataDto>(await _contentMetadataRepository.Create(_mapper.Map<ContentMetadata>(contentMetadata)));
        }

        public async Task<ContentMetadataDto> UpdateContentMetadata(ContentMetadataDto contentMetadata)
        {
            return _mapper.Map<ContentMetadataDto>(await _contentMetadataRepository.Update(_mapper.Map<ContentMetadata>(contentMetadata)));
        }

        public async Task CreateContentImage(ContentImageDto contentImage)
        {
            await _contentImageRepository.Create(_mapper.Map<ContentImage>(contentImage));
        }

        public async Task DeleteAllContentImages(int contentId)
        {
            await _contentRepository.DeleteAllContentImages(contentId);
        }

        public List<ContentImageDto> GetAllContentImages(int contentId)
        {
            return _mapper.Map<List<ContentImageDto>>(_contentRepository.GetAllContentImages(contentId));
        }

        public async Task DeleteSection(int sectionId)
        {
            await _contentSectionRepository.Delete(sectionId);
        }

        public async Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId)
        {
            return await _contentRepository.GetContentsInCategory(categoryId, applicationId);
        }

        public List<ContentApiDto> GetContentInCategoryAsBox(int categoryId)
        {
            return _contentRepository.GetContentInCategoryAsBox(categoryId);
        }

        public async Task UpdateSectionPriority(int sectionId, int priority)
        {
            await _contentRepository.UpdateSectionPriority(sectionId, priority);
        }
    }
}
