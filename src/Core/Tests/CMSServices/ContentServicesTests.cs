using AutoMapper;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Infrastructure.Dto.CMSDtos;
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

    private static ContentServices CreateSut(Mock<IContentRepository> contentRepository, IMapper mapper = null)
    {
        return new ContentServices(
            contentRepository.Object,
            Mock.Of<IRepository<ContentSection>>(),
            Mock.Of<IRepository<SectionElement>>(),
            Mock.Of<IRepository<ContentMetadata>>(),
            Mock.Of<IRepository<ContentImage>>(),
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
    public async Task Update_MapsDtoToEntity_AndReturnsMappedResult()
    {
        var contentRepository = new Mock<IContentRepository>();
        contentRepository
            .Setup(r => r.Update(It.Is<Content>(c => c.Id == 7 && c.Title == "Updated Title")))
            .ReturnsAsync((Content c) => c);

        var sut = CreateSut(contentRepository);

        var result = await sut.Update(new ContentDto { Id = 7, Title = "Updated Title" });

        Assert.Equal("Updated Title", result.Title);
        contentRepository.Verify(r => r.Update(It.IsAny<Content>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTranslate_SetsFarsiContent_AndPreservesOtherFields()
    {
        var existing = new Content
        {
            Id = 5,
            Title = "Original Title",
            Abstract = "Original Abstract",
            FarsiContent = null
        };

        var contentRepository = new Mock<IContentRepository>();
        contentRepository.Setup(r => r.GetById(5)).ReturnsAsync(existing);
        contentRepository.Setup(r => r.Update(It.IsAny<Content>())).ReturnsAsync((Content c) => c);

        var sut = CreateSut(contentRepository);

        await sut.UpdateTranslate(5, "{\"title\":\"ترجمه\"}");

        contentRepository.Verify(r => r.Update(It.Is<Content>(c =>
            c.FarsiContent == "{\"title\":\"ترجمه\"}" &&
            c.Title == "Original Title" &&
            c.Abstract == "Original Abstract")), Times.Once);
    }

    [Fact]
    public async Task Delete_DelegatesToRepository()
    {
        var contentRepository = new Mock<IContentRepository>();
        var sut = CreateSut(contentRepository);

        await sut.Delete(9);

        contentRepository.Verify(r => r.Delete(9), Times.Once);
    }
}
