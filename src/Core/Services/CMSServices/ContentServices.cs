using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.CMS;
using Application.Contracts.CMSApi;
using Domains.Entities.ContentManagement;
using Application.Repository;
using Application.CMSRepository;
using Application.GeneralRepository;

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
        private readonly ICategoryRepository _categoryRepository;
        private readonly ITagRepository _tagRepository;
        private readonly ICultureRepository _cultureRepository;
        private readonly IMapper _mapper;

        // constructor
        public ContentServices(IContentRepository contentRepository, IRepository<ContentSection> contentSectionRepository,
        IRepository<SectionElement> sectionElementRepository, IRepository<ContentMetadata> contentMetadataRepository, IRepository<ContentImage> contentImageRepository,
        ICategoryRepository categoryRepository, ITagRepository tagRepository, ICultureRepository cultureRepository,
        IMapper mapper)
        {
            _contentRepository = contentRepository;
            _contentSectionRepository = contentSectionRepository;
            _sectionElementRepository = sectionElementRepository;
            _contentMetadataRepository = contentMetadataRepository;
            _contentImageRepository = contentImageRepository;
            _categoryRepository = categoryRepository;
            _tagRepository = tagRepository;
            _cultureRepository = cultureRepository;
            _mapper = mapper;
        }

        // methods
        public async Task<ContentDto> Create(ContentDto content)
        {
            return _mapper.Map<ContentDto>(await _contentRepository.Create(_mapper.Map<Content>(content)));
        }

        public async Task Delete(int id, int applicationId)
        {
            // Throws if id doesn't exist or belongs to another application, before any delete happens.
            await _contentRepository.GetByIdForApplication(id, applicationId);
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

        public async Task ChangeContentActiveMode(int id, bool mode, int applicationId)
        {
            var content = await _contentRepository.GetByIdForApplication(id, applicationId);
            content.IsActive = mode;
            await _contentRepository.Update(content);
        }

        public async Task UpdateTranslate(int contentId, string translatedContent, int applicationId)
        {
            // Confirms application ownership first. UpdateFarsiContent itself still queries
            // without AsNoTracking (rather than going through GetByIdForApplication, which is
            // AsNoTracking), so it resolves to a caller's already-tracked instance instead of
            // conflicting with it — see IContentProvider.GetContentForTranslate.
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
            await _contentRepository.UpdateFarsiContent(contentId, translatedContent);
        }

        public async Task ActivateTranslatedContent(int contentId, string translatedContent, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
            await _contentRepository.ActivateTranslatedContent(contentId, translatedContent);
        }

        public async Task<ContentDto> Update(ContentDto content, int applicationId)
        {
            // Confirms the content being edited already belongs to this application, and
            // re-pins ApplicationId server-side so this call can't be used to move content into
            // a different application.
            await _contentRepository.GetByIdForApplication(content.Id, applicationId);
            content.ApplicationId = applicationId;
            return _mapper.Map<ContentDto>(await _contentRepository.Update(_mapper.Map<Content>(content)));
        }

        public async Task<ContentDto> GetById(int id, int applicationId)
        {
            return _mapper.Map<ContentDto>(await _contentRepository.GetByIdForApplication(id, applicationId));
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

        public async Task<List<SectionDto>> GetSections(int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);

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

        public async Task CreateContentCategories(List<int> categoryIds, int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);

            var distinctIds = (categoryIds ?? new List<int>()).Distinct().ToList();
            if (distinctIds.Count > 0)
            {
                var validIds = _categoryRepository.List(applicationId).Select(c => c.Id).ToHashSet();
                if (distinctIds.Any(id => !validIds.Contains(id)))
                    throw new ArgumentException("One or more category ids are invalid or belong to a different application.");
            }

            await _contentRepository.CreateContentCategories(contentId, distinctIds);
        }

        public async Task CreateContentTags(List<int> tagIds, int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);

            var distinctIds = (tagIds ?? new List<int>()).Distinct().ToList();
            if (distinctIds.Count > 0)
            {
                var validIds = _tagRepository.List(applicationId).Select(t => t.Id).ToHashSet();
                if (distinctIds.Any(id => !validIds.Contains(id)))
                    throw new ArgumentException("One or more tag ids are invalid or belong to a different application.");
            }

            await _contentRepository.CreateContentTags(contentId, distinctIds);
        }

        public async Task CreateContentCultures(List<int> cultureIds, int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);

            var distinctIds = (cultureIds ?? new List<int>()).Distinct().ToList();
            if (distinctIds.Count > 0)
            {
                // Culture is a global lookup in this codebase today (not scoped to an
                // application anywhere else — see CultureServices.List()), so this only rejects
                // ids that don't exist, without an application-match requirement.
                var validIds = _cultureRepository.List().Select(c => c.Id).ToHashSet();
                if (distinctIds.Any(id => !validIds.Contains(id)))
                    throw new ArgumentException("One or more culture ids are invalid.");
            }

            await _contentRepository.CreateContentCultures(contentId, distinctIds);
        }

        public async Task<ContentMetadataDto> GetContentMetadata(int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
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

        public async Task DeleteAllContentImages(int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
            await _contentRepository.DeleteAllContentImages(contentId);
        }

        public async Task<List<ContentImageDto>> GetAllContentImages(int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
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
