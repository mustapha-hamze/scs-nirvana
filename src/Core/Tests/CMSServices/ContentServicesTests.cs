using AutoMapper;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.GeneralRepository;
using Application.Contracts.CMS;
using Application.UnitOfWork;
using Infrastructure.Mapper;
using Application.Mapper;
using Moq;
using Application.UseCases.CMSServices;
using Xunit;

namespace Core.Tests.CMSServices;

public class ContentServicesTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); });
        return config.CreateMapper();
    }

    private static ContentServices CreateSut(
        Mock<IContentQueryRepository> contentQueryRepository = null,
        Mock<IContentCommandRepository> contentCommandRepository = null,
        Mock<IContentRelationRepository> contentRelationRepository = null,
        Mock<IContentsInCategoryQueryAdapter> contentsInCategoryQueryAdapter = null,
        Mock<ICategoryRepository> categoryRepository = null,
        Mock<ITagRepository> tagRepository = null,
        Mock<ICultureRepository> cultureRepository = null,
        IMapper mapper = null,
        Mock<IUnitOfWork> unitOfWork = null)
    {
        var command = contentCommandRepository ?? new Mock<IContentCommandRepository>();
        // Same default the real repository gives UpdateContentMetadata: echoes back whatever
        // entity it was handed, so callers that merge onto the loaded entity see it round-trip.
        command.Setup(r => r.UpdateContentMetadata(It.IsAny<ContentMetadata>())).ReturnsAsync((ContentMetadata m) => m);

        return new ContentServices(
            (contentQueryRepository ?? new Mock<IContentQueryRepository>()).Object,
            command.Object,
            (contentRelationRepository ?? new Mock<IContentRelationRepository>()).Object,
            (contentsInCategoryQueryAdapter ?? new Mock<IContentsInCategoryQueryAdapter>()).Object,
            (categoryRepository ?? new Mock<ICategoryRepository>()).Object,
            (tagRepository ?? new Mock<ITagRepository>()).Object,
            (cultureRepository ?? new Mock<ICultureRepository>()).Object,
            mapper ?? CreateMapper(),
            (unitOfWork ?? DefaultUnitOfWork()).Object);
    }

    private static Mock<IUnitOfWork> DefaultUnitOfWork()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        // Runs the operation inline, same as the real UnitOfWork, so callers composing a
        // transaction around a repository call (e.g. CreateContentCategories) still execute it.
        unitOfWork.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((operation, _) => operation());
        return unitOfWork;
    }

    [Fact]
    public async Task Create_MapsDtoToEntity_AndReturnsMappedResult()
    {
        var contentCommandRepository = new Mock<IContentCommandRepository>();
        contentCommandRepository
            .Setup(r => r.Create(It.Is<Content>(c => c.Title == "New Content")))
            .ReturnsAsync((Content c) => { c.Id = 42; return c; });

        var sut = CreateSut(contentCommandRepository: contentCommandRepository);

        var result = await sut.Create(new ContentDto { Title = "New Content", TypeId = 1000 });

        Assert.Equal(42, result.Id);
        Assert.Equal("New Content", result.Title);
        contentCommandRepository.Verify(r => r.Create(It.IsAny<Content>()), Times.Once);
    }

    [Fact]
    public async Task Create_IgnoresCategoriesTagsCulturesFromDto()
    {
        // ContentInCategory/Tag/Culture are canonical; a normal create must never let a client
        // set these compatibility strings directly — only the dedicated
        // CreateContentCategories/Tags/Cultures commands may, and only alongside the join rows.
        var contentCommandRepository = new Mock<IContentCommandRepository>();
        contentCommandRepository
            .Setup(r => r.Create(It.IsAny<Content>()))
            .ReturnsAsync((Content c) => { c.Id = 1; return c; });

        var sut = CreateSut(contentCommandRepository: contentCommandRepository);

        await sut.Create(new ContentDto { Title = "New", TypeId = 1000, Categories = "1|2", Tags = "3", Cultures = "4" });

        contentCommandRepository.Verify(r => r.Create(It.Is<Content>(
            c => c.Categories == null && c.Tags == null && c.Cultures == null)), Times.Once);
    }

    [Fact]
    public async Task Update_ContentBelongsToApplication_MapsDtoToEntity_AndReturnsMappedResult()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository
            .Setup(r => r.GetByIdForApplication(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Content { Id = 7, ApplicationId = 1 });
        var contentCommandRepository = new Mock<IContentCommandRepository>();
        contentCommandRepository
            .Setup(r => r.Update(It.Is<Content>(c => c.Id == 7 && c.Title == "Updated Title" && c.ApplicationId == 1)))
            .ReturnsAsync((Content c) => c);

        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        var result = await sut.Update(new ContentDto { Id = 7, Title = "Updated Title" }, applicationId: 1);

        Assert.Equal("Updated Title", result.Title);
        contentCommandRepository.Verify(r => r.Update(It.IsAny<Content>()), Times.Once);
    }

    [Fact]
    public async Task Update_PreservesFieldsNotCarriedByDto()
    {
        // ContentDto has no FarsiContent property. Update must merge onto the loaded entity so
        // this field (and anything else ContentDto doesn't expose) keeps its stored value instead
        // of being wiped by the blind entity-wide write.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository
            .Setup(r => r.GetByIdForApplication(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Content { Id = 7, ApplicationId = 1, FarsiContent = "ترجمه موجود" });
        var contentCommandRepository = new Mock<IContentCommandRepository>();
        contentCommandRepository
            .Setup(r => r.Update(It.IsAny<Content>()))
            .ReturnsAsync((Content c) => c);

        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        await sut.Update(new ContentDto { Id = 7, Title = "Updated Title" }, applicationId: 1);

        contentCommandRepository.Verify(r => r.Update(It.Is<Content>(c => c.FarsiContent == "ترجمه موجود")), Times.Once);
    }

    [Fact]
    public async Task Update_DoesNotDesyncCategoriesTagsCultures_EvenWhenDtoCarriesDifferentValues()
    {
        // Categories/Tags/Cultures are canonical via the join tables; a normal update — including
        // a partial DTO that only means to change Title — must never let a different (stale or
        // malicious) compatibility string from the DTO win over what's actually stored.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository
            .Setup(r => r.GetByIdForApplication(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Content { Id = 7, ApplicationId = 1, Categories = "1|2", Tags = "5", Cultures = "9" });
        var contentCommandRepository = new Mock<IContentCommandRepository>();
        contentCommandRepository
            .Setup(r => r.Update(It.IsAny<Content>()))
            .ReturnsAsync((Content c) => c);

        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        var result = await sut.Update(
            new ContentDto { Id = 7, Title = "Updated", Categories = "99", Tags = "99", Cultures = "99" },
            applicationId: 1);

        Assert.Equal("1|2", result.Categories);
        Assert.Equal("5", result.Tags);
        Assert.Equal("9", result.Cultures);
        contentCommandRepository.Verify(r => r.Update(It.Is<Content>(
            c => c.Categories == "1|2" && c.Tags == "5" && c.Cultures == "9")), Times.Once);
    }

    [Fact]
    public async Task Update_CommitsExactlyOnce()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository
            .Setup(r => r.GetByIdForApplication(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Content { Id = 7, ApplicationId = 1 });
        var contentCommandRepository = new Mock<IContentCommandRepository>();
        contentCommandRepository
            .Setup(r => r.Update(It.IsAny<Content>()))
            .ReturnsAsync((Content c) => c);

        var unitOfWork = new Mock<IUnitOfWork>();
        var sut = CreateSut(contentQueryRepository, contentCommandRepository, unitOfWork: unitOfWork);

        await sut.Update(new ContentDto { Id = 7, Title = "Updated Title" }, applicationId: 1);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_ContentBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Cross-application mutation attempt: caller is in application 1, the content belongs
        // to application 2. Must be rejected before Update ever runs.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository
            .Setup(r => r.GetByIdForApplication(7, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var contentCommandRepository = new Mock<IContentCommandRepository>();
        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.Update(new ContentDto { Id = 7, Title = "Hijacked" }, applicationId: 1));

        contentCommandRepository.Verify(r => r.Update(It.IsAny<Content>()), Times.Never);
    }

    [Fact]
    public async Task GetById_IgnoredApplicationId_StillEnforced()
    {
        // A caller that doesn't bother passing a real applicationId (e.g. 0, or any id it
        // doesn't own) must not be able to read another application's content.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository
            .Setup(r => r.GetByIdForApplication(7, 0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentQueryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetById(7, applicationId: 0));
    }

    [Fact]
    public async Task UpdateTranslate_ContentBelongsToApplication_DelegatesToUpdateFarsiContent()
    {
        // UpdateTranslate must go through the non-AsNoTracking repository method rather than
        // GetById (AsNoTracking) + Update, since the latter throws when the caller already
        // holds a tracked Content instance for this id in the same request (e.g. from
        // IContentProvider.GetContentForTranslate).
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(5, 1, It.IsAny<CancellationToken>())).ReturnsAsync(new Content { Id = 5, ApplicationId = 1 });
        var contentCommandRepository = new Mock<IContentCommandRepository>();
        contentCommandRepository.Setup(r => r.UpdateFarsiContent(5, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        await sut.UpdateTranslate(5, "{\"title\":\"ترجمه\"}", applicationId: 1);

        contentCommandRepository.Verify(r => r.UpdateFarsiContent(5, "{\"title\":\"ترجمه\"}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTranslate_CrossApplicationContentId_ThrowsAndDoesNotWrite()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(5, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var contentCommandRepository = new Mock<IContentCommandRepository>();
        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.UpdateTranslate(5, "malicious", applicationId: 1));

        contentCommandRepository.Verify(r => r.UpdateFarsiContent(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateTranslatedContent_ContentBelongsToApplication_DelegatesToRepository()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(5, 1, It.IsAny<CancellationToken>())).ReturnsAsync(new Content { Id = 5, ApplicationId = 1 });
        var contentCommandRepository = new Mock<IContentCommandRepository>();
        contentCommandRepository.Setup(r => r.ActivateTranslatedContent(5, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        await sut.ActivateTranslatedContent(5, "translated", applicationId: 1);

        contentCommandRepository.Verify(r => r.ActivateTranslatedContent(5, "translated", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ContentBelongsToApplication_DelegatesToRepository()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(9, 1, It.IsAny<CancellationToken>())).ReturnsAsync(new Content { Id = 9, ApplicationId = 1 });
        var contentCommandRepository = new Mock<IContentCommandRepository>();

        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        await sut.Delete(9, applicationId: 1);

        contentCommandRepository.Verify(r => r.Delete(9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_CrossApplicationId_ThrowsAndDoesNotDelete()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(9, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());
        var contentCommandRepository = new Mock<IContentCommandRepository>();

        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.Delete(9, applicationId: 1));

        contentCommandRepository.Verify(r => r.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateContentCategories_DeduplicatesAndPassesValidatedIds()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(3, 1, It.IsAny<CancellationToken>())).ReturnsAsync(new Content { Id = 3, ApplicationId = 1 });
        var contentRelationRepository = new Mock<IContentRelationRepository>();
        contentRelationRepository.Setup(r => r.CreateContentCategories(3, It.IsAny<List<int>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository.Setup(r => r.List(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Category>
        {
            new() { Id = 10, ApplicationId = 1 },
            new() { Id = 11, ApplicationId = 1 },
        });

        var sut = CreateSut(contentQueryRepository, contentRelationRepository: contentRelationRepository, categoryRepository: categoryRepository);

        await sut.CreateContentCategories(new List<int> { 10, 11, 10 }, contentId: 3, applicationId: 1);

        contentRelationRepository.Verify(r => r.CreateContentCategories(3, It.Is<List<int>>(
            ids => ids.Count == 2 && ids.Contains(10) && ids.Contains(11)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateContentCategories_CrossApplicationCategoryId_ThrowsAndDoesNotWrite()
    {
        // categoryId 99 belongs to a different application than the content being edited: the
        // whole write must be rejected, not silently filtered.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(3, 1, It.IsAny<CancellationToken>())).ReturnsAsync(new Content { Id = 3, ApplicationId = 1 });
        var contentRelationRepository = new Mock<IContentRelationRepository>();

        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository.Setup(r => r.List(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Category> { new() { Id = 10, ApplicationId = 1 } });

        var sut = CreateSut(contentQueryRepository, contentRelationRepository: contentRelationRepository, categoryRepository: categoryRepository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreateContentCategories(new List<int> { 10, 99 }, contentId: 3, applicationId: 1));

        contentRelationRepository.Verify(r => r.CreateContentCategories(It.IsAny<int>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateContentCategories_ContentInDifferentApplication_ThrowsBeforeValidatingCategories()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(3, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());
        var contentRelationRepository = new Mock<IContentRelationRepository>();

        var categoryRepository = new Mock<ICategoryRepository>();

        var sut = CreateSut(contentQueryRepository, contentRelationRepository: contentRelationRepository, categoryRepository: categoryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateContentCategories(new List<int> { 10 }, contentId: 3, applicationId: 1));

        categoryRepository.Verify(r => r.List(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        contentRelationRepository.Verify(r => r.CreateContentCategories(It.IsAny<int>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateSection_ContentBelongsToApplication_Creates()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(3, 1, It.IsAny<CancellationToken>())).ReturnsAsync(new Content { Id = 3, ApplicationId = 1 });

        var sut = CreateSut(contentQueryRepository);

        await sut.CreateSection(new SectionDto { ContentId = 3, Priority = 1 }, applicationId: 1);

        contentQueryRepository.Verify(r => r.GetByIdForApplication(3, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSection_ContentBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Never trust ContentId from the DTO: a section can't be attached to another
        // application's content just because the caller supplied that ContentId.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(3, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentQueryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateSection(new SectionDto { ContentId = 3, Priority = 1 }, applicationId: 1));
    }

    [Fact]
    public async Task CreateSectionElement_SectionBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Never trust SectionId from the DTO.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetSectionForApplication(4, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentQueryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateSectionElement(new SectionElementDto { SectionId = 4 }, applicationId: 1));
    }

    [Fact]
    public async Task UpdateSectionElement_ElementBelongsToApplication_MergesFieldsOntoLoadedEntity()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetElementForApplication(8, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SectionElement { Id = 8, SectionId = 4, TinyText = "old" });

        var sut = CreateSut(contentQueryRepository);

        await sut.UpdateSectionElement(new SectionElementDto { Id = 8, TinyText = "new" }, applicationId: 1);

        contentQueryRepository.Verify(r => r.GetElementForApplication(8, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSectionElement_ElementBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Never trust the element's own Id without resolving element -> section -> content ->
        // application first: a caller in application 1 must not edit application 2's element.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetElementForApplication(8, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentQueryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.UpdateSectionElement(new SectionElementDto { Id = 8, TinyText = "hijacked" }, applicationId: 1));
    }

    [Fact]
    public async Task DeleteSection_SectionBelongsToDifferentApplication_ThrowsAndDoesNotDelete()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetSectionForApplication(4, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentQueryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.DeleteSection(4, applicationId: 1));
    }

    [Fact]
    public async Task UpdateSectionPriority_SectionBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetSectionForApplication(4, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());
        var contentCommandRepository = new Mock<IContentCommandRepository>();

        var sut = CreateSut(contentQueryRepository, contentCommandRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateSectionPriority(4, 2, applicationId: 1));

        contentCommandRepository.Verify(r => r.UpdateSectionPriority(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateContentMetadata_ContentBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(3, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentQueryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateContentMetadata(new ContentMetadataDto { ContentId = 3 }, applicationId: 1));
    }

    [Fact]
    public async Task UpdateContentMetadata_MetadataBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        // Never trust contentMetadata.ContentId from the DTO: ownership is resolved from the
        // existing row (by its own Id), not from whatever ContentId the caller supplied.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetContentMetadataForApplication(6, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentQueryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.UpdateContentMetadata(new ContentMetadataDto { Id = 6, ContentId = 999 }, applicationId: 1));
    }

    [Fact]
    public async Task UpdateContentMetadata_RepinsContentIdToVerifiedValue()
    {
        // Even though the caller's DTO claims ContentId 999, the verified/loaded value must win.
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetContentMetadataForApplication(6, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentMetadata { Id = 6, ContentId = 3, Title = "Old" });

        var sut = CreateSut(contentQueryRepository);

        var result = await sut.UpdateContentMetadata(new ContentMetadataDto { Id = 6, ContentId = 999, Title = "New" }, applicationId: 1);

        Assert.Equal(3, result.ContentId);
    }

    [Fact]
    public async Task CreateContentImage_ContentBelongsToDifferentApplication_ThrowsAndDoesNotWrite()
    {
        var contentQueryRepository = new Mock<IContentQueryRepository>();
        contentQueryRepository.Setup(r => r.GetByIdForApplication(3, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(contentQueryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.CreateContentImage(new ContentImageDto { ContentId = 3 }, applicationId: 1));
    }
}
