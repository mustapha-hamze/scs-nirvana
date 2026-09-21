using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Application.Contracts.CMS;
using Application.Contracts.CMSApi;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.GeneralRepository;
using Application.UnitOfWork;

namespace Services.CMSServices
{
    public class ContentServices : IContentServices
    {
        // fields
        private readonly IContentRepository _contentRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ITagRepository _tagRepository;
        private readonly ICultureRepository _cultureRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // constructor
        public ContentServices(IContentRepository contentRepository,
        ICategoryRepository categoryRepository, ITagRepository tagRepository, ICultureRepository cultureRepository,
        IMapper mapper, IUnitOfWork unitOfWork)
        {
            _contentRepository = contentRepository;
            _categoryRepository = categoryRepository;
            _tagRepository = tagRepository;
            _cultureRepository = cultureRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task<ContentDto> Create(ContentDto content)
        {
            var created = await _contentRepository.Create(_mapper.Map<Content>(content));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<ContentDto>(created);
        }

        public async Task Delete(int id, int applicationId)
        {
            // Throws if id doesn't exist or belongs to another application, before any delete happens.
            await _contentRepository.GetByIdForApplication(id, applicationId);
            await _contentRepository.Delete(id);
            await _unitOfWork.SaveChangesAsync();
        }

        public List<ContentApiDto> GetContentByIdFull(int id, int applicationId)
        {
            return _contentRepository.GetContentByIdFull(id, applicationId);
        }

        public List<ContentApiDto> GetContentByTypeId(int typeId, int applicationId)
        {
            return _contentRepository.GetContentByTypeId(typeId, applicationId);
        }

        public BlogIndexApiDto GetContentByTypeId(int typeId, int applicationId, int pageIndex = 1)
        {
            return _contentRepository.GetContentByTypeId(typeId, applicationId, pageIndex);
        }

        public BlogIndexApiDto GetContentByCategoryId(int categoryId, int applicationId, int pageIndex = 1, int pageSize = 40)
        {
            return _contentRepository.GetContentByCategoryId(categoryId, applicationId, pageIndex, pageSize);
        }

        public BlogIndexApiDto GetContentByCategoryIdByDate(int categoryId, int applicationId, DateTime startDate, DateTime endDate, int pageIndex)
        {
            return _contentRepository.GetContentByCategoryIdByDate(categoryId, applicationId, startDate, endDate, pageIndex);
        }

        public async Task ChangeContentActiveMode(int id, bool mode, int applicationId)
        {
            var content = await _contentRepository.GetByIdForApplication(id, applicationId);
            content.IsActive = mode;
            await _contentRepository.Update(content);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task UpdateTranslate(int contentId, string translatedContent, int applicationId)
        {
            // Confirms application ownership first. UpdateFarsiContent itself still queries
            // without AsNoTracking (rather than going through GetByIdForApplication, which is
            // AsNoTracking), so it resolves to a caller's already-tracked instance instead of
            // conflicting with it — see IContentProvider.GetContentForTranslate.
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
            await _contentRepository.UpdateFarsiContent(contentId, translatedContent);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task ActivateTranslatedContent(int contentId, string translatedContent, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
            await _contentRepository.ActivateTranslatedContent(contentId, translatedContent);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<ContentDto> Update(ContentDto content, int applicationId)
        {
            // Confirms the content being edited already belongs to this application, and
            // re-pins ApplicationId server-side so this call can't be used to move content into
            // a different application. Maps onto the loaded entity (not a fresh one) so fields
            // ContentDto doesn't carry — e.g. FarsiContent — keep their existing value instead of
            // being cleared by the blind entity-wide update.
            var existing = await _contentRepository.GetByIdForApplication(content.Id, applicationId);
            content.ApplicationId = applicationId;
            _mapper.Map(content, existing);
            var updated = await _contentRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<ContentDto>(updated);
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

        public async Task<SectionDto> CreateSection(SectionDto section, int applicationId)
        {
            // Never trust ContentId from the DTO — verify it belongs to this application first.
            await _contentRepository.GetByIdForApplication(section.ContentId, applicationId);

            var created = await _contentRepository.CreateSection(_mapper.Map<ContentSection>(section));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<SectionDto>(created);
        }

        public async Task<SectionElementDto> CreateSectionElement(SectionElementDto sectionElement, int applicationId)
        {
            // Never trust SectionId from the DTO — verify the section and its content belong to
            // this application (and neither is soft-deleted) first.
            await _contentRepository.GetSectionForApplication(sectionElement.SectionId, applicationId);

            var created = await _contentRepository.CreateSectionElement(_mapper.Map<SectionElement>(sectionElement));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<SectionElementDto>(created);
        }

        public async Task UpdateSectionElement(SectionElementDto sectionElement, int applicationId)
        {
            // Resolves ownership through the actual chain (element -> section -> content ->
            // application) rather than trusting the DTO or a bare GetById; throws if the element,
            // its section, or its content is missing, soft-deleted, or belongs to another application.
            var element = await _contentRepository.GetElementForApplication(sectionElement.Id, applicationId);

            element.EditorText = sectionElement.EditorText;
            element.FileNameText = sectionElement.FileNameText;
            element.GalleryImages = sectionElement.GalleryImages;
            element.TinyText = sectionElement.TinyText;

            element.UpdatedDT = DateTime.Now;

            await _contentRepository.UpdateElement(element);
            await _unitOfWork.SaveChangesAsync();
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

            // The repository only stages the join-row replace and the legacy compatibility
            // string; this transaction is what makes both change together or not at all.
            await _unitOfWork.ExecuteInTransactionAsync(() => _contentRepository.CreateContentCategories(contentId, distinctIds));
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

            await _unitOfWork.ExecuteInTransactionAsync(() => _contentRepository.CreateContentTags(contentId, distinctIds));
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

            await _unitOfWork.ExecuteInTransactionAsync(() => _contentRepository.CreateContentCultures(contentId, distinctIds));
        }

        public async Task<ContentMetadataDto> GetContentMetadata(int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
            return _mapper.Map<ContentMetadataDto>(_contentRepository.GetContentMetadata(contentId));
        }

        public async Task<ContentMetadataDto> CreateContentMetadata(ContentMetadataDto contentMetadata, int applicationId)
        {
            // Never trust ContentId from the DTO — verify it belongs to this application first.
            await _contentRepository.GetByIdForApplication(contentMetadata.ContentId, applicationId);

            contentMetadata.IsActive = true;
            var created = await _contentRepository.CreateContentMetadata(_mapper.Map<ContentMetadata>(contentMetadata));
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<ContentMetadataDto>(created);
        }

        public async Task<ContentMetadataDto> UpdateContentMetadata(ContentMetadataDto contentMetadata, int applicationId)
        {
            // Resolves the existing row through the application-scoped chain (metadata -> content
            // -> application) rather than trusting contentMetadata.ContentId from the DTO, and
            // re-pins ContentId to the verified value so this call can't be used to reattach the
            // metadata to a different content.
            var existing = await _contentRepository.GetContentMetadataForApplication(contentMetadata.Id, applicationId);
            contentMetadata.ContentId = existing.ContentId;
            _mapper.Map(contentMetadata, existing);

            var updated = await _contentRepository.UpdateContentMetadata(existing);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<ContentMetadataDto>(updated);
        }

        public async Task CreateContentImage(ContentImageDto contentImage, int applicationId)
        {
            // Never trust ContentId from the DTO — verify it belongs to this application first.
            await _contentRepository.GetByIdForApplication(contentImage.ContentId, applicationId);

            await _contentRepository.CreateContentImage(_mapper.Map<ContentImage>(contentImage));
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteAllContentImages(int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
            await _contentRepository.DeleteAllContentImages(contentId);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<ContentImageDto>> GetAllContentImages(int contentId, int applicationId)
        {
            await _contentRepository.GetByIdForApplication(contentId, applicationId);
            return _mapper.Map<List<ContentImageDto>>(_contentRepository.GetAllContentImages(contentId));
        }

        public async Task DeleteSection(int sectionId, int applicationId)
        {
            await _contentRepository.GetSectionForApplication(sectionId, applicationId);
            await _contentRepository.DeleteSection(sectionId);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId)
        {
            // Missing, deleted, or wrong-application category: same empty result as "no content
            // matched" — the stored procedure this delegates to has no application/category
            // ownership check of its own, so it's gated here instead.
            var isOwnedByApplication = _categoryRepository.List(applicationId).Any(c => c.Id == categoryId && c.IsActive);
            if (!isOwnedByApplication)
                return new List<ContentDto>();

            return await _contentRepository.GetContentsInCategory(categoryId, applicationId);
        }

        public List<ContentApiDto> GetContentInCategoryAsBox(int categoryId, int applicationId)
        {
            return _contentRepository.GetContentInCategoryAsBox(categoryId, applicationId);
        }

        public async Task UpdateSectionPriority(int sectionId, int priority, int applicationId)
        {
            await _contentRepository.GetSectionForApplication(sectionId, applicationId);
            await _contentRepository.UpdateSectionPriority(sectionId, priority);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
