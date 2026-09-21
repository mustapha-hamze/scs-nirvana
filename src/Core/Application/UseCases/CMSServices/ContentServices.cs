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

namespace Application.UseCases.CMSServices
{
    public class ContentServices : IContentServices
    {
        // fields
        private readonly IContentQueryRepository _contentQueryRepository;
        private readonly IContentCommandRepository _contentCommandRepository;
        private readonly IContentRelationRepository _contentRelationRepository;
        private readonly IContentsInCategoryQueryAdapter _contentsInCategoryQueryAdapter;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ITagRepository _tagRepository;
        private readonly ICultureRepository _cultureRepository;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;

        // constructor
        public ContentServices(IContentQueryRepository contentQueryRepository, IContentCommandRepository contentCommandRepository,
        IContentRelationRepository contentRelationRepository, IContentsInCategoryQueryAdapter contentsInCategoryQueryAdapter,
        ICategoryRepository categoryRepository, ITagRepository tagRepository, ICultureRepository cultureRepository,
        IMapper mapper, IUnitOfWork unitOfWork)
        {
            _contentQueryRepository = contentQueryRepository;
            _contentCommandRepository = contentCommandRepository;
            _contentRelationRepository = contentRelationRepository;
            _contentsInCategoryQueryAdapter = contentsInCategoryQueryAdapter;
            _categoryRepository = categoryRepository;
            _tagRepository = tagRepository;
            _cultureRepository = cultureRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        // methods
        public async Task<ContentDto> Create(ContentDto content, CancellationToken cancellationToken = default)
        {
            var created = await _contentCommandRepository.Create(_mapper.Map<Content>(content));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<ContentDto>(created);
        }

        public async Task Delete(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            // Throws if id doesn't exist or belongs to another application, before any delete happens.
            await _contentQueryRepository.GetByIdForApplication(id, applicationId, cancellationToken);
            await _contentCommandRepository.Delete(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public Task<List<ContentApiDto>> GetContentByIdFull(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return _contentQueryRepository.GetContentByIdFull(id, applicationId, cancellationToken);
        }

        public Task<List<ContentApiDto>> GetContentByTypeId(int typeId, int applicationId, CancellationToken cancellationToken = default)
        {
            return _contentQueryRepository.GetContentByTypeId(typeId, applicationId, cancellationToken);
        }

        public Task<BlogIndexApiDto> GetContentByTypeId(int typeId, int applicationId, int pageIndex = 1, CancellationToken cancellationToken = default)
        {
            return _contentQueryRepository.GetContentByTypeId(typeId, applicationId, pageIndex, cancellationToken);
        }

        public Task<BlogIndexApiDto> GetContentByCategoryId(int categoryId, int applicationId, int pageIndex = 1, int pageSize = 40, CancellationToken cancellationToken = default)
        {
            return _contentQueryRepository.GetContentByCategoryId(categoryId, applicationId, pageIndex, pageSize, cancellationToken);
        }

        public Task<BlogIndexApiDto> GetContentByCategoryIdByDate(int categoryId, int applicationId, DateTime startDate, DateTime endDate, int pageIndex, CancellationToken cancellationToken = default)
        {
            return _contentQueryRepository.GetContentByCategoryIdByDate(categoryId, applicationId, startDate, endDate, pageIndex, cancellationToken);
        }

        public async Task ChangeContentActiveMode(int id, bool mode, int applicationId, CancellationToken cancellationToken = default)
        {
            var content = await _contentQueryRepository.GetByIdForApplication(id, applicationId, cancellationToken);
            content.IsActive = mode;
            await _contentCommandRepository.Update(content);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateTranslate(int contentId, string translatedContent, int applicationId, CancellationToken cancellationToken = default)
        {
            // Confirms application ownership first. UpdateFarsiContent itself still queries
            // without AsNoTracking (rather than going through GetByIdForApplication, which is
            // AsNoTracking), so it resolves to a caller's already-tracked instance instead of
            // conflicting with it — see IContentProvider.GetContentForTranslate.
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);
            await _contentCommandRepository.UpdateFarsiContent(contentId, translatedContent, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task ActivateTranslatedContent(int contentId, string translatedContent, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);
            await _contentCommandRepository.ActivateTranslatedContent(contentId, translatedContent, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<ContentDto> Update(ContentDto content, int applicationId, CancellationToken cancellationToken = default)
        {
            // Confirms the content being edited already belongs to this application, and
            // re-pins ApplicationId server-side so this call can't be used to move content into
            // a different application. Maps onto the loaded entity (not a fresh one) so fields
            // ContentDto doesn't carry — e.g. FarsiContent — keep their existing value instead of
            // being cleared by the blind entity-wide update.
            var existing = await _contentQueryRepository.GetByIdForApplication(content.Id, applicationId, cancellationToken);
            content.ApplicationId = applicationId;
            _mapper.Map(content, existing);
            var updated = await _contentCommandRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<ContentDto>(updated);
        }

        public async Task<ContentDto> GetById(int id, int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<ContentDto>(await _contentQueryRepository.GetByIdForApplication(id, applicationId, cancellationToken));
        }

        public async Task<List<ContentDto>> List(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<ContentDto>>(await _contentQueryRepository.List(applicationId, cancellationToken));
        }

        public async Task<List<ContentDto>> List(int applicationId, int pageIndex, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<ContentDto>>(await _contentQueryRepository.List(applicationId, pageIndex, cancellationToken));
        }

        public async Task<List<ContentDto>> OurBlogBoxList(int applicationId, CancellationToken cancellationToken = default)
        {
            return _mapper.Map<List<ContentDto>>(await _contentQueryRepository.OurBlogBoxList(applicationId, cancellationToken));
        }

        public async Task<SectionDto> CreateSection(SectionDto section, int applicationId, CancellationToken cancellationToken = default)
        {
            // Never trust ContentId from the DTO — verify it belongs to this application first.
            await _contentQueryRepository.GetByIdForApplication(section.ContentId, applicationId, cancellationToken);

            var created = await _contentCommandRepository.CreateSection(_mapper.Map<ContentSection>(section));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SectionDto>(created);
        }

        public async Task<SectionElementDto> CreateSectionElement(SectionElementDto sectionElement, int applicationId, CancellationToken cancellationToken = default)
        {
            // Never trust SectionId from the DTO — verify the section and its content belong to
            // this application (and neither is soft-deleted) first.
            await _contentQueryRepository.GetSectionForApplication(sectionElement.SectionId, applicationId, cancellationToken);

            var created = await _contentCommandRepository.CreateSectionElement(_mapper.Map<SectionElement>(sectionElement));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SectionElementDto>(created);
        }

        public async Task UpdateSectionElement(SectionElementDto sectionElement, int applicationId, CancellationToken cancellationToken = default)
        {
            // Resolves ownership through the actual chain (element -> section -> content ->
            // application) rather than trusting the DTO or a bare GetById; throws if the element,
            // its section, or its content is missing, soft-deleted, or belongs to another application.
            var element = await _contentQueryRepository.GetElementForApplication(sectionElement.Id, applicationId, cancellationToken);

            element.EditorText = sectionElement.EditorText;
            element.FileNameText = sectionElement.FileNameText;
            element.GalleryImages = sectionElement.GalleryImages;
            element.TinyText = sectionElement.TinyText;

            await _contentCommandRepository.UpdateElement(element);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<SectionDto>> GetSections(int contentId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);

            var sections = await _contentQueryRepository.GetContentSections(contentId, cancellationToken);

            var elementsBySectionId = (await _contentQueryRepository.GetSectionElements(sections.Select(s => s.Id).ToList(), cancellationToken))
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

        public Task<int> ContentCount(int applicationId, CancellationToken cancellationToken = default)
        {
            return _contentQueryRepository.ContentCount(applicationId, cancellationToken);
        }

        public async Task CreateContentCategories(List<int> categoryIds, int contentId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);

            var distinctIds = (categoryIds ?? new List<int>()).Distinct().ToList();
            if (distinctIds.Count > 0)
            {
                var validIds = (await _categoryRepository.List(applicationId, cancellationToken)).Select(c => c.Id).ToHashSet();
                if (distinctIds.Any(id => !validIds.Contains(id)))
                    throw new ArgumentException("One or more category ids are invalid or belong to a different application.");
            }

            // The repository only stages the join-row replace and the legacy compatibility
            // string; this transaction is what makes both change together or not at all.
            await _unitOfWork.ExecuteInTransactionAsync(() => _contentRelationRepository.CreateContentCategories(contentId, distinctIds, cancellationToken), cancellationToken);
        }

        public async Task CreateContentTags(List<int> tagIds, int contentId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);

            var distinctIds = (tagIds ?? new List<int>()).Distinct().ToList();
            if (distinctIds.Count > 0)
            {
                var validIds = (await _tagRepository.List(applicationId, cancellationToken)).Select(t => t.Id).ToHashSet();
                if (distinctIds.Any(id => !validIds.Contains(id)))
                    throw new ArgumentException("One or more tag ids are invalid or belong to a different application.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(() => _contentRelationRepository.CreateContentTags(contentId, distinctIds, cancellationToken), cancellationToken);
        }

        public async Task CreateContentCultures(List<int> cultureIds, int contentId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);

            var distinctIds = (cultureIds ?? new List<int>()).Distinct().ToList();
            if (distinctIds.Count > 0)
            {
                // Culture is a global lookup in this codebase today (not scoped to an
                // application anywhere else — see CultureServices.List()), so this only rejects
                // ids that don't exist, without an application-match requirement.
                var validIds = (await _cultureRepository.List(cancellationToken)).Select(c => c.Id).ToHashSet();
                if (distinctIds.Any(id => !validIds.Contains(id)))
                    throw new ArgumentException("One or more culture ids are invalid.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(() => _contentRelationRepository.CreateContentCultures(contentId, distinctIds, cancellationToken), cancellationToken);
        }

        public async Task<ContentMetadataDto> GetContentMetadata(int contentId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);
            return _mapper.Map<ContentMetadataDto>(await _contentQueryRepository.GetContentMetadata(contentId, cancellationToken));
        }

        public async Task<ContentMetadataDto> CreateContentMetadata(ContentMetadataDto contentMetadata, int applicationId, CancellationToken cancellationToken = default)
        {
            // Never trust ContentId from the DTO — verify it belongs to this application first.
            await _contentQueryRepository.GetByIdForApplication(contentMetadata.ContentId, applicationId, cancellationToken);

            contentMetadata.IsActive = true;
            var created = await _contentCommandRepository.CreateContentMetadata(_mapper.Map<ContentMetadata>(contentMetadata));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<ContentMetadataDto>(created);
        }

        public async Task<ContentMetadataDto> UpdateContentMetadata(ContentMetadataDto contentMetadata, int applicationId, CancellationToken cancellationToken = default)
        {
            // Resolves the existing row through the application-scoped chain (metadata -> content
            // -> application) rather than trusting contentMetadata.ContentId from the DTO, and
            // re-pins ContentId to the verified value so this call can't be used to reattach the
            // metadata to a different content.
            var existing = await _contentQueryRepository.GetContentMetadataForApplication(contentMetadata.Id, applicationId, cancellationToken);
            contentMetadata.ContentId = existing.ContentId;
            _mapper.Map(contentMetadata, existing);

            var updated = await _contentCommandRepository.UpdateContentMetadata(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<ContentMetadataDto>(updated);
        }

        public async Task CreateContentImage(ContentImageDto contentImage, int applicationId, CancellationToken cancellationToken = default)
        {
            // Never trust ContentId from the DTO — verify it belongs to this application first.
            await _contentQueryRepository.GetByIdForApplication(contentImage.ContentId, applicationId, cancellationToken);

            await _contentCommandRepository.CreateContentImage(_mapper.Map<ContentImage>(contentImage));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAllContentImages(int contentId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);
            await _contentCommandRepository.DeleteAllContentImages(contentId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<ContentImageDto>> GetAllContentImages(int contentId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetByIdForApplication(contentId, applicationId, cancellationToken);
            return _mapper.Map<List<ContentImageDto>>(await _contentQueryRepository.GetAllContentImages(contentId, cancellationToken));
        }

        public async Task DeleteSection(int sectionId, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetSectionForApplication(sectionId, applicationId, cancellationToken);
            await _contentCommandRepository.DeleteSection(sectionId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<ContentDto>> GetContentsInCategory(int categoryId, int applicationId, CancellationToken cancellationToken = default)
        {
            // Missing, deleted, or wrong-application category: same empty result as "no content
            // matched" — the stored procedure this delegates to has no application/category
            // ownership check of its own, so it's gated here instead.
            var categories = await _categoryRepository.List(applicationId, cancellationToken);
            var isOwnedByApplication = categories.Any(c => c.Id == categoryId && c.IsActive);
            if (!isOwnedByApplication)
                return new List<ContentDto>();

            return await _contentsInCategoryQueryAdapter.GetContentsInCategory(categoryId, applicationId, cancellationToken);
        }

        public Task<List<ContentApiDto>> GetContentInCategoryAsBox(int categoryId, int applicationId, CancellationToken cancellationToken = default)
        {
            return _contentQueryRepository.GetContentInCategoryAsBox(categoryId, applicationId, cancellationToken);
        }

        public async Task UpdateSectionPriority(int sectionId, int priority, int applicationId, CancellationToken cancellationToken = default)
        {
            await _contentQueryRepository.GetSectionForApplication(sectionId, applicationId, cancellationToken);
            await _contentCommandRepository.UpdateSectionPriority(sectionId, priority, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}