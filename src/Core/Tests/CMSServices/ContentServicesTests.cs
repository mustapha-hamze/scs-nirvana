using AutoMapper;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.GeneralRepository;
using Application.Contracts.CMS;
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
        IMapper mapper = null)
    {
        return new ContentServices(
            contentRepository.Object,
            Mock.Of<IRepository<ContentSection>>(),
            Mock.Of<IRepository<SectionElement>>(),
            Mock.Of<IRepository<ContentMetadata>>(),
            Mock.Of<IRepository<ContentImage>>(),
            (categoryRepository ?? new Mock<ICategoryRepository>()).Object,
            (tagRepository ?? new Mock<ITagRepository>()).Object,
            (cultureRepository ?? new Mock<ICultureRepository>()).Object,
            mapper ?? CreateMapper());
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
}
