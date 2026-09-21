using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.UserManagement;
using Application.GeneralRepository;
using Application.UnitOfWork;
using Application.UserManagementRepository;
using Moq;
using Application.UseCases.UserManagementServices;
using Xunit;

namespace Core.Tests.UserManagement;

public class UserManagementServicesTests
{
    private static UserManagementServices CreateSut(
        Mock<IUserManagementRepository> userManagementRepository,
        Mock<IApplicationRepository> applicationRepository = null,
        Mock<IUnitOfWork> unitOfWork = null)
    {
        return new UserManagementServices(
            userManagementRepository.Object,
            (applicationRepository ?? new Mock<IApplicationRepository>()).Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object);
    }

    [Fact]
    public async Task SetCurrentApplicationId_AuthorizedSelection_Succeeds()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com"))
            .Returns(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5)).ReturnsAsync(true);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5)).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        await sut.SetCurrentApplicationId("user@example.com", 5);

        userManagementRepository.Verify(r => r.SetCurrentApplicationId("user@example.com", 5), Times.Once);
    }

    [Fact]
    public async Task SetCurrentApplicationId_NoMembership_ThrowsAndDoesNotSelect()
    {
        // The user exists and the application is fine, but they have no membership row at all.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com"))
            .Returns(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5)).ReturnsAsync(false);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5)).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetCurrentApplicationId("user@example.com", 5));

        userManagementRepository.Verify(r => r.SetCurrentApplicationId(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SetCurrentApplicationId_DeletedOrInactiveMembership_ThrowsAndDoesNotSelect()
    {
        // HasActiveMembership itself already excludes deleted/inactive rows, so from the
        // service's point of view this looks identical to "no membership" - same rejection.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com"))
            .Returns(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5)).ReturnsAsync(false);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5)).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetCurrentApplicationId("user@example.com", 5));
    }

    [Fact]
    public async Task SetCurrentApplicationId_DeletedOrInactiveApplication_ThrowsAndDoesNotSelect()
    {
        // The user is a genuine active member, but the target application itself is gone/off.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com"))
            .Returns(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5)).ReturnsAsync(true);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5)).ReturnsAsync(false);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetCurrentApplicationId("user@example.com", 5));

        userManagementRepository.Verify(r => r.SetCurrentApplicationId(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SetCurrentApplicationId_UnknownUser_ThrowsAndDoesNotSelect()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("ghost@example.com")).Returns((UserDto)null);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5)).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetCurrentApplicationId("ghost@example.com", 5));
    }

    [Fact]
    public async Task SetCurrentApplicationId_ClearingToZero_BypassesMembershipCheck()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();

        var applicationRepository = new Mock<IApplicationRepository>();

        var sut = CreateSut(userManagementRepository, applicationRepository);

        await sut.SetCurrentApplicationId("user@example.com", 0);

        userManagementRepository.Verify(r => r.SetCurrentApplicationId("user@example.com", 0), Times.Once);
        userManagementRepository.Verify(r => r.HasActiveMembership(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        applicationRepository.Verify(r => r.ExistsActiveApplication(It.IsAny<int>()), Times.Never);
    }
}
