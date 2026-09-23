using Application.Contracts.UserManagement;
using Application.GeneralRepository;
using Application.UserManagementRepository;
using Application.UseCases.Tenancy;
using Moq;
using Xunit;

namespace Core.Tests.UserManagement;

public class TenantAccessGuardTests
{
    private static TenantAccessGuard CreateSut(
        Mock<IUserManagementRepository> userManagementRepository,
        Mock<IApplicationRepository> applicationRepository)
    {
        return new TenantAccessGuard(userManagementRepository.Object, applicationRepository.Object);
    }

    [Fact]
    public async Task HasAccessAsync_ActiveMembershipAndActiveApplication_ReturnsTrue()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        Assert.True(await sut.HasAccessAsync("user@example.com", 5));
    }

    [Fact]
    public async Task HasAccessAsync_RevokedMembership_ReturnsFalse()
    {
        // The membership row was removed entirely (e.g. the user was kicked from the app).
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        Assert.False(await sut.HasAccessAsync("user@example.com", 5));
    }

    [Fact]
    public async Task HasAccessAsync_InactiveOrDeletedMembership_ReturnsFalse()
    {
        // HasActiveMembership already excludes inactive/deleted rows, so from the guard's point
        // of view this is identical to a revoked/missing membership.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        Assert.False(await sut.HasAccessAsync("user@example.com", 5));
    }

    [Fact]
    public async Task HasAccessAsync_InactiveOrDeletedApplication_ReturnsFalse()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        Assert.False(await sut.HasAccessAsync("user@example.com", 5));
    }

    [Fact]
    public async Task HasAccessAsync_UnknownUser_ReturnsFalse()
    {
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("ghost@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto)null);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        Assert.False(await sut.HasAccessAsync("ghost@example.com", 5));
    }

    [Fact]
    public async Task HasAccessAsync_SuperAdminWithNoMembership_ReturnsTrueForActiveApplication()
    {
        // No membership row is set up at all - GetUserByEmailAddress/HasActiveMembership are
        // never even stubbed. A SuperAdmin must not need one.
        var userManagementRepository = new Mock<IUserManagementRepository>();

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        Assert.True(await sut.HasAccessAsync("superadmin@example.com", 5, isSuperAdmin: true));

        userManagementRepository.Verify(r => r.GetUserByEmailAddress(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        userManagementRepository.Verify(r => r.HasActiveMembership(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HasAccessAsync_SuperAdminWithInactiveOrDeletedApplication_ReturnsFalse()
    {
        // SuperAdmin bypasses membership, never the application's own active/non-deleted state.
        var userManagementRepository = new Mock<IUserManagementRepository>();

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        Assert.False(await sut.HasAccessAsync("superadmin@example.com", 5, isSuperAdmin: true));
    }

    [Fact]
    public async Task HasAccessAsync_NonSuperAdminDefault_StillRequiresMembership()
    {
        // isSuperAdmin defaults to false - every pre-existing (non-SuperAdmin-aware) caller keeps
        // today's membership-only rule unchanged.
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.GetUserByEmailAddress("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto { Id = "u1" });
        userManagementRepository.Setup(r => r.HasActiveMembership("u1", 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.ExistsActiveApplication(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = CreateSut(userManagementRepository, applicationRepository);

        Assert.False(await sut.HasAccessAsync("user@example.com", 5));
    }
}
