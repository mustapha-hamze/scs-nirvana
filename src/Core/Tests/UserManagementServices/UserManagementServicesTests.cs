using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Contracts.Tenancy;
using Application.Contracts.UserManagement;
using Application.GeneralRepository;
using Application.UnitOfWork;
using Application.UserManagementRepository;
using Moq;
using Application.UseCases.Tenancy;
using Application.UseCases.UserManagementServices;
using Xunit;

namespace Core.Tests.UserManagement;

public class UserManagementServicesTests
{
    // A minimal stand-in for a browser session's tenant selection, so tests can assert against
    // plain state instead of mocking a two-way property.
    private class FakeCurrentApplicationContext : ICurrentApplicationContext
    {
        public int? CurrentApplicationId { get; set; }
    }

    private static UserManagementServices CreateSut(
        Mock<IUserManagementRepository> userManagementRepository,
        Mock<IApplicationRepository> applicationRepository = null,
        Mock<IUnitOfWork> unitOfWork = null,
        ICurrentApplicationContext currentApplicationContext = null)
    {
        return new UserManagementServices(
            userManagementRepository.Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object,
            currentApplicationContext ?? new FakeCurrentApplicationContext(),
            new TenantAccessGuard(userManagementRepository.Object, (applicationRepository ?? new Mock<IApplicationRepository>()).Object));
    }

    [Fact]
    public async Task SetCurrentApplicationId_AuthorizedSelection_Succeeds()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var currentApplicationContext = new FakeCurrentApplicationContext();
        var sut = CreateSut(userManagementRepository, applicationRepository, currentApplicationContext: currentApplicationContext);

        await sut.SetCurrentApplicationId("user@example.com", 5);

        Assert.Equal(5, currentApplicationContext.CurrentApplicationId);
    }

    [Fact]
    public async Task SetCurrentApplicationId_NoMembership_ThrowsAndDoesNotSelect()
    {
        // The user exists and the application is fine, but they have no membership row at all.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var currentApplicationContext = new FakeCurrentApplicationContext();
        var sut = CreateSut(userManagementRepository, applicationRepository, currentApplicationContext: currentApplicationContext);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetCurrentApplicationId("user@example.com", 5));

        Assert.Null(currentApplicationContext.CurrentApplicationId);
    }

    [Fact]
    public async Task SetCurrentApplicationId_DeletedOrInactiveMembership_ThrowsAndDoesNotSelect()
    {
        // HasActiveMembership itself already excludes deleted/inactive rows, so from the
        // service's point of view this looks identical to "no membership" - same rejection.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetCurrentApplicationId("user@example.com", 5));
    }

    [Fact]
    public async Task SetCurrentApplicationId_DeletedOrInactiveApplication_ThrowsAndDoesNotSelect()
    {
        // The user is a genuine active member, but the target application itself is gone/off -
        // this also covers a "missing" (non-existent) application id, which is indistinguishable
        // from inactive/deleted at this check.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var currentApplicationContext = new FakeCurrentApplicationContext();
        var sut = CreateSut(userManagementRepository, applicationRepository, currentApplicationContext: currentApplicationContext);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetCurrentApplicationId("user@example.com", 5));

        Assert.Null(currentApplicationContext.CurrentApplicationId);
    }

    [Fact]
    public async Task SetCurrentApplicationId_UnknownUser_ThrowsAndDoesNotSelect()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("ghost@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((UserDto)null);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetCurrentApplicationId("ghost@example.com", 5));
    }

    [Fact]
    public async Task SetCurrentApplicationId_ClearingToZero_BypassesMembershipCheck()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();

        var applicationRepository = new Mock<IApplicationRepository>();

        var currentApplicationContext = new FakeCurrentApplicationContext { CurrentApplicationId = 5 };
        var sut = CreateSut(userManagementRepository, applicationRepository, currentApplicationContext: currentApplicationContext);

        await sut.SetCurrentApplicationId("user@example.com", 0);

        Assert.Null(currentApplicationContext.CurrentApplicationId);
        userManagementRepository.Verify(r => r.HasActiveMembership(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        applicationRepository.Verify(r => r.ExistsActiveApplication(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetCurrentApplicationId_TwoIndependentContexts_SelectionInOneDoesNotAffectTheOther()
    {
        // Simulates two browser sessions for the same account: selecting an application through
        // one session's context must never be visible through the other.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sessionA = new FakeCurrentApplicationContext();
        var sessionB = new FakeCurrentApplicationContext();
        var sutForSessionA = CreateSut(userManagementRepository, applicationRepository, currentApplicationContext: sessionA);

        await sutForSessionA.SetCurrentApplicationId("user@example.com", 5);

        Assert.Equal(5, sessionA.CurrentApplicationId);
        Assert.Null(sessionB.CurrentApplicationId);
    }
}
