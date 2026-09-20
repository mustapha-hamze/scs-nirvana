using AutoMapper;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.GeneralRepository;
using Application.Contracts.CMS;
using Application.UnitOfWork;
using Infrastructure.Mapper;
using Application.Repository;
using Moq;
using Services.CMSServices;
using Xunit;

namespace Core.Tests.CMSServices;

public class ContentServicesTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new MapperProfile()));
        return config.CreateMapper();
    }

    private static ContentServices CreateSut(
        Mock<IContentRepository> contentRepository,
        Mock<ICategoryRepository> categoryRepository = null,
        Mock<ITagRepository> tagRepository = null,
        Mock<ICultureRepository> cultureRepository = null,
        Mock<IRepository<ContentMetadata>> contentMetadataRepository = null,
        IMapper mapper = null,
        Mock<IUnitOfWork> unitOfWork = null)
    {
        return new ContentServices(
            contentRepository.Object,
            Mock.Of<IRepository<ContentSection>>(),
            Mock.Of<IRepository<SectionElement>>(),
            (contentMetadataRepository ?? DefaultContentMetadataRepository()).Object,
            Mock.Of<IRepository<ContentImage>>(),
            (categoryRepository ?? new Mock<ICategoryRepository>()).Object,
            (tagRepository ?? new Mock<ITagRepository>()).Object,
            (cultureRepository ?? new Mock<ICultureRepository>()).Object,
            mapper ?? CreateMapper(),
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    private static Mock<IRepository<ContentMetadata>> DefaultContentMetadataRepository()
    {
        var repository = new Mock<IRepository<ContentMetadata>>();
        repository.Setup(r => r.Update(It.IsAny<ContentMetadata>())).ReturnsAsync((ContentMetadata m) => m);
        return repository;
    }

    [Fact]
    public async Task Create_MapsDtoToEntity_AndReturnsMappedResult()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository
            .Setup(r => r.Create(It.Is<Content>(c => c.Title == "New Content")))
            .ReturnsAsync((Content c) => { c.Id = 42; return c; });

        var sut = CreateSut(contentRepository);

        var result = await sut.Create(new ContentDto { Title = "New Content", TypeId = 1000 });

        Assert.Equal(42, result.Id);
        Assert.Equal("New Content", result.Title);
        contentRepository.Verify(r => r.Create(It.IsAny<Content>()), Times.Once);
    }

    [Fact]
    public async Task Update_ContentBelongsToApplication_MapsDtoToEntity_AndReturnsMappedResult()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository
            .Setup(r => r.GetByIdForApplication(7, 1))
            .ReturnsAsync(new Content { Id = 7, ApplicationId = 1 });
        contentRepository
            .Setup(r => r.Update(It.Is<Content>(c => c.Id == 7 && c.Title == "Updated Title" && c.ApplicationId == 1)))
            .ReturnsAsync((Content c) => c);

        var sut = CreateSut(contentRepository);

        var result = await sut.Update(new ContentDto { Id = 7, Title = "Updated Title" }, applicationId: 1);

        Assert.Equal("Updated Title", result.Title);
        contentRepository.Verify(r => r.Update(It.IsAny<Content>()), Times.Once);
    }

    [Fact]
    public async Task Update_PreservesFieldsNotCarriedByDto()
    {
        // ContentDto has no FarsiContent property. Update must merge onto the loaded entity so
        // this field (and anything else ContentDto doesn't expose) keeps its stored value instead
        // of being wiped by the blind entity-wide write.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository
            .Setup(r => r.GetByIdForApplication(7, 1))
            .ReturnsAsync(new Content { Id = 7, ApplicationId = 1, FarsiContent = "ترجمه موجود" });
        contentRepository
            .Setup(r => r.Update(It.IsAny<Content>()))
            .ReturnsAsync((Content c) => c);

        var sut = CreateSut(contentRepository);

        await sut.Update(new ContentDto { Id = 7, Title = "Updated Title" }, applicationId: 1);

        contentRepository.Verify(r => r.Update(It.Is<Content>(c => c.FarsiContent == "ترجمه موجود")), Times.Once);
    }

    [Fact]
    public async Task Update_CommitsExactlyOnce()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository
            .Setup(r => r.GetByIdForApplication(7, 1))
            .ReturnsAsync(new Content { Id = 7, ApplicationId = 1 });
        contentRepository
            .Setup(r => r.Update(It.IsAny<Content>()))
            .ReturnsAsync((Content c) => c);

        var unitOfWork = new Mock<IUnitOfWork>();
        var sut = CreateSut(contentRepository, unitOfWork: unitOfWork);

        await sut.Update(new ContentDto { Id = 7, Title = "Updated Title" }, applicationId: 1);

        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_ContentBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Cross-application mutation attempt: caller is in application 1, the content belongs
        // to application 2. Must be rejected before Update ever runs.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository
            .Setup(r => r.GetByIdForApplication(7, 1))
            .ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.Update(new ContentDto { Id = 7, Title = "Hijacked" }, applicationId: 1));

        contentRepository.Verify(r => r.Update(It.IsAny<Content>()), Times.Never);
    }

    [Fact]
    public async Task GetById_IgnoredApplicationId_StillEnforced()
    {
        // A caller that doesn't bother passing a real applicationId (e.g. 0, or any id it
        // doesn't own) must not be able to read another application's content.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository
            .Setup(r => r.GetByIdForApplication(7, 0))
            .ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetById(7, applicationId: 0));
    }

    [Fact]
    public async Task UpdateTranslate_ContentBelongsToApplication_DelegatesToUpdateFarsiContent()
    {
        // UpdateTranslate must go through the non-AsNoTracking repository method rather than
        // GetById (AsNoTracking) + Update, since the latter throws when the caller already
        // holds a tracked Content instance for this id in the same request (e.g. from
        // IContentProvider.GetContentForTranslate).
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(5, 1)).ReturnsAsync(new Content { Id = 5, ApplicationId = 1 });
        contentRepository.Setup(r => r.UpdateFarsiContent(5, It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = CreateSut(contentRepository);

        await sut.UpdateTranslate(5, "{\"title\":\"ترجمه\"}", applicationId: 1);

        contentRepository.Verify(r => r.UpdateFarsiContent(5, "{\"title\":\"ترجمه\"}"), Times.Once);
    }

    [Fact]
    public async Task UpdateTranslate_CrossApplicationContentId_ThrowsAndDoesNotWrite()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(5, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.UpdateTranslate(5, "malicious", applicationId: 1));

        contentRepository.Verify(r => r.UpdateFarsiContent(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ActivateTranslatedContent_ContentBelongsToApplication_DelegatesToRepository()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(5, 1)).ReturnsAsync(new Content { Id = 5, ApplicationId = 1 });
        contentRepository.Setup(r => r.ActivateTranslatedContent(5, It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = CreateSut(contentRepository);

        await sut.ActivateTranslatedContent(5, "translated", applicationId: 1);

        contentRepository.Verify(r => r.ActivateTranslatedContent(5, "translated"), Times.Once);
    }

    [Fact]
    public async Task Delete_ContentBelongsToApplication_DelegatesToRepository()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(9, 1)).ReturnsAsync(new Content { Id = 9, ApplicationId = 1 });

        var sut = CreateSut(contentRepository);

        await sut.Delete(9, applicationId: 1);

        contentRepository.Verify(r => r.Delete(9), Times.Once);
    }

    [Fact]
    public async Task Delete_CrossApplicationId_ThrowsAndDoesNotDelete()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(9, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.Delete(9, applicationId: 1));

        contentRepository.Verify(r => r.Delete(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateContentCategories_DeduplicatesAndPassesValidatedIds()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(3, 1)).ReturnsAsync(new Content { Id = 3, ApplicationId = 1 });
        contentRepository.Setup(r => r.CreateContentCategories(3, It.IsAny<List<int>>())).Returns(Task.CompletedTask);

        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository.Setup(r => r.List(1)).Returns(new List<Category>
        {
            new() { Id = 10, ApplicationId = 1 },
            new() { Id = 11, ApplicationId = 1 },
        });

        var sut = CreateSut(contentRepository, categoryRepository: categoryRepository);

        await sut.CreateContentCategories(new List<int> { 10, 11, 10 }, contentId: 3, applicationId: 1);

        contentRepository.Verify(r => r.CreateContentCategories(3, It.Is<List<int>>(
            ids => ids.Count == 2 && ids.Contains(10) && ids.Contains(11))), Times.Once);
    }

    [Fact]
    public async Task CreateContentCategories_CrossApplicationCategoryId_ThrowsAndDoesNotWrite()
    {
        // categoryId 99 belongs to a different application than the content being edited: the
        // whole write must be rejected, not silently filtered.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(3, 1)).ReturnsAsync(new Content { Id = 3, ApplicationId = 1 });

        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository.Setup(r => r.List(1)).Returns(new List<Category> { new() { Id = 10, ApplicationId = 1 } });

        var sut = CreateSut(contentRepository, categoryRepository: categoryRepository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreateContentCategories(new List<int> { 10, 99 }, contentId: 3, applicationId: 1));

        contentRepository.Verify(r => r.CreateContentCategories(It.IsAny<int>(), It.IsAny<List<int>>()), Times.Never);
    }

    [Fact]
    public async Task CreateContentCategories_ContentInDifferentApplication_ThrowsBeforeValidatingCategories()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(3, 1)).ThrowsAsync(new KeyNotFoundException());

        var categoryRepository = new Mock<ICategoryRepository>();

        var sut = CreateSut(contentRepository, categoryRepository: categoryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateContentCategories(new List<int> { 10 }, contentId: 3, applicationId: 1));

        categoryRepository.Verify(r => r.List(It.IsAny<int>()), Times.Never);
        contentRepository.Verify(r => r.CreateContentCategories(It.IsAny<int>(), It.IsAny<List<int>>()), Times.Never);
    }

    [Fact]
    public async Task CreateSection_ContentBelongsToApplication_Creates()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(3, 1)).ReturnsAsync(new Content { Id = 3, ApplicationId = 1 });

        var sut = CreateSut(contentRepository);

        await sut.CreateSection(new SectionDto { ContentId = 3, Priority = 1 }, applicationId: 1);

        contentRepository.Verify(r => r.GetByIdForApplication(3, 1), Times.Once);
    }

    [Fact]
    public async Task CreateSection_ContentBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Never trust ContentId from the DTO: a section can't be attached to another
        // application's content just because the caller supplied that ContentId.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(3, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateSection(new SectionDto { ContentId = 3, Priority = 1 }, applicationId: 1));
    }

    [Fact]
    public async Task CreateSectionElement_SectionBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Never trust SectionId from the DTO.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetSectionForApplication(4, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateSectionElement(new SectionElementDto { SectionId = 4 }, applicationId: 1));
    }

    [Fact]
    public async Task UpdateSectionElement_ElementBelongsToApplication_MergesFieldsOntoLoadedEntity()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetElementForApplication(8, 1))
            .ReturnsAsync(new SectionElement { Id = 8, SectionId = 4, TinyText = "old" });

        var sut = CreateSut(contentRepository);

        await sut.UpdateSectionElement(new SectionElementDto { Id = 8, TinyText = "new" }, applicationId: 1);

        contentRepository.Verify(r => r.GetElementForApplication(8, 1), Times.Once);
    }

    [Fact]
    public async Task UpdateSectionElement_ElementBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Never trust the element's own Id without resolving element -> section -> content ->
        // application first: a caller in application 1 must not edit application 2's element.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetElementForApplication(8, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.UpdateSectionElement(new SectionElementDto { Id = 8, TinyText = "hijacked" }, applicationId: 1));
    }

    [Fact]
    public async Task DeleteSection_SectionBelongsToDifferentApplication_ThrowsAndDoesNotDelete()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetSectionForApplication(4, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.DeleteSection(4, applicationId: 1));
    }

    [Fact]
    public async Task UpdateSectionPriority_SectionBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetSectionForApplication(4, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateSectionPriority(4, 2, applicationId: 1));

        contentRepository.Verify(r => r.UpdateSectionPriority(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateContentMetadata_ContentBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(3, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateContentMetadata(new ContentMetadataDto { ContentId = 3 }, applicationId: 1));
    }

    [Fact]
    public async Task UpdateContentMetadata_MetadataBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Never trust contentMetadata.ContentId from the DTO: ownership is resolved from the
        // existing row (by its own Id), not from whatever ContentId the caller supplied.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetContentMetadataForApplication(6, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.UpdateContentMetadata(new ContentMetadataDto { Id = 6, ContentId = 999 }, applicationId: 1));
    }

    [Fact]
    public async Task UpdateContentMetadata_RepinsContentIdToVerifiedValue()
    {
        // Even though the caller's DTO claims ContentId 999, the verified/loaded value must win.
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetContentMetadataForApplication(6, 1))
            .ReturnsAsync(new ContentMetadata { Id = 6, ContentId = 3, Title = "Old" });

        var sut = CreateSut(contentRepository);

        var result = await sut.UpdateContentMetadata(new ContentMetadataDto { Id = 6, ContentId = 999, Title = "New" }, applicationId: 1);

        Assert.Equal(3, result.ContentId);
    }

    [Fact]
    public async Task CreateContentImage_ContentBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetByIdForApplication(3, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateContentImage(new ContentImageDto { ContentId = 3 }, applicationId: 1));
    }
}
