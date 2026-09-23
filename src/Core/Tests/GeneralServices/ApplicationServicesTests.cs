using AutoMapper;
using Domains.Entities.General;
using Application.GeneralRepository;
using Application.Contracts.General;
using Application.UnitOfWork;
using Infrastructure.Mapper;
using Application.Mapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Application.UseCases.GeneralServices;
using Xunit;

namespace Core.Tests.GeneralServices;

public class ApplicationServicesTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static ApplicationServices CreateSut(Mock<IApplicationRepository> applicationRepository)
    {
        return new ApplicationServices(applicationRepository.Object, CreateMapper(), new Mock<IUnitOfWork>().Object);
    }

    [Fact]
    public async Task CreateApplicationSetting_ApplicationIdTampering_ServerPinsRealApplicationId()
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.CreateApplicationSetting(It.IsAny<ApplicationSetting>()))
            .ReturnsAsync((ApplicationSetting s) => s);

        var sut = CreateSut(applicationRepository);

        var result = await sut.CreateApplicationSetting(new ApplicationSettingDto { ApplicationId = 99, Title = "X" }, applicationId: 1);

        Assert.Equal(1, result.ApplicationId);
        applicationRepository.Verify(r => r.CreateApplicationSetting(It.Is<ApplicationSetting>(s => s.ApplicationId == 1)), Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromApplication_DelegatesApplicationIdToRepository()
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.RemoveUserFromApplication(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = CreateSut(applicationRepository);

        await sut.RemoveUserFromApplication(relationId: 7, applicationId: 1);

        applicationRepository.Verify(r => r.RemoveUserFromApplication(7, 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
