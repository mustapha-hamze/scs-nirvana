using AutoMapper;
using Domains.Entities.General;
using Application.GeneralRepository;
using Application.Contracts.General;
using Application.UnitOfWork;
using Infrastructure.Mapper;
using Application.Mapper;
using Moq;
using Application.UseCases.GeneralServices;
using Xunit;

namespace Core.Tests.GeneralServices;

public class TagServicesTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); });
        return config.CreateMapper();
    }

    private static TagServices CreateSut(Mock<ITagRepository> tagRepository, Mock<IUnitOfWork> unitOfWork = null)
    {
        return new TagServices(tagRepository.Object, CreateMapper(), (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    [Fact]
    public async Task Create_ApplicationIdTampering_ServerPinsRealApplicationId()
    {
        var tagRepository = new Mock<ITagRepository>();
        tagRepository.Setup(r => r.Create(It.IsAny<Tag>())).ReturnsAsync((Tag t) => t);

        var sut = CreateSut(tagRepository);

        var result = await sut.Create(new TagDto { ApplicationId = 99, Title = "New" }, applicationId: 1);

        Assert.Equal(1, result.ApplicationId);
        tagRepository.Verify(r => r.Create(It.Is<Tag>(t => t.ApplicationId == 1)), Times.Once);
    }

    [Fact]
    public async Task GetById_CrossApplication_Throws()
    {
        var tagRepository = new Mock<ITagRepository>();
        tagRepository.Setup(r => r.GetByIdForApplication(5, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(tagRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetById(5, applicationId: 1));
    }

    [Fact]
    public async Task Delete_CrossApplication_ThrowsAndDoesNotDelete()
    {
        var tagRepository = new Mock<ITagRepository>();
        tagRepository.Setup(r => r.GetByIdForApplication(5, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(tagRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.Delete(5, applicationId: 1));

        tagRepository.Verify(r => r.Delete(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
