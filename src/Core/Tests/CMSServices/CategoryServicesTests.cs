using AutoMapper;
using Domains.Entities.ContentManagement;
using Application.CMSRepository;
using Application.Contracts.CMS;
using Application.UnitOfWork;
using Infrastructure.Mapper;
using Application.Mapper;
using Moq;
using Application.UseCases.CMSServices;
using Xunit;

namespace Core.Tests.CMSServices;

public class CategoryServicesTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); });
        return config.CreateMapper();
    }

    private static CategoryServices CreateSut(Mock<ICategoryRepository> categoryRepository, Mock<IUnitOfWork> unitOfWork = null)
    {
        return new CategoryServices(categoryRepository.Object, CreateMapper(), (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    [Fact]
    public async Task Create_ApplicationIdTampering_ServerPinsRealApplicationId()
    {
        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository.Setup(r => r.Create(It.IsAny<Category>())).ReturnsAsync((Category c) => c);

        var sut = CreateSut(categoryRepository);

        var result = await sut.Create(new CategoryDto { ApplicationId = 99, Title = "New" }, applicationId: 1);

        Assert.Equal(1, result.ApplicationId);
        categoryRepository.Verify(r => r.Create(It.Is<Category>(c => c.ApplicationId == 1)), Times.Once);
    }

    [Fact]
    public async Task GetById_CrossApplication_Throws()
    {
        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository.Setup(r => r.GetByIdForApplication(5, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(categoryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetById(5, applicationId: 1));
    }

    [Fact]
    public async Task Delete_CrossApplication_ThrowsAndDoesNotDelete()
    {
        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository.Setup(r => r.GetByIdForApplication(5, 1)).ThrowsAsync(new KeyNotFoundException());

        var sut = CreateSut(categoryRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.Delete(5, applicationId: 1));

        categoryRepository.Verify(r => r.Delete(It.IsAny<int>()), Times.Never);
    }
}
