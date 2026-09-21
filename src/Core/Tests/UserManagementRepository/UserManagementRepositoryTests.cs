using AutoMapper;
using Core.Tests.TestSupport;
using Domains.Entities.General;
using Infrastructure.Mapper;
using Application.Mapper;
using Infrastructure.UserManagementRepository;
using Xunit;

namespace Core.Tests.UserManagementRepository;

public class UserManagementRepositoryTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); });
        return config.CreateMapper();
    }

    [Fact]
    public async Task HasActiveMembership_ActiveNonDeletedRow_ReturnsTrue()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.UserInApplications.Add(new UserInApplication { UserId = "u1", ApplicationId = 5, IsActive = true });
        context.SaveChanges();

        var repository = new Infrastructure.UserManagementRepository.UserManagementRepository(context, CreateMapper());

        Assert.True(await repository.HasActiveMembership("u1", 5));
    }

    [Fact]
    public async Task HasActiveMembership_NoRow_ReturnsFalse()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var repository = new Infrastructure.UserManagementRepository.UserManagementRepository(context, CreateMapper());

        Assert.False(await repository.HasActiveMembership("u1", 5));
    }

    [Fact]
    public async Task HasActiveMembership_SoftDeletedRow_ReturnsFalse()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.UserInApplications.Add(new UserInApplication { UserId = "u1", ApplicationId = 5, IsActive = true, IsDeleted = true });
        context.SaveChanges();

        var repository = new Infrastructure.UserManagementRepository.UserManagementRepository(context, CreateMapper());

        Assert.False(await repository.HasActiveMembership("u1", 5));
    }

    [Fact]
    public async Task HasActiveMembership_InactiveRow_ReturnsFalse()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.UserInApplications.Add(new UserInApplication { UserId = "u1", ApplicationId = 5, IsActive = false });
        context.SaveChanges();

        var repository = new Infrastructure.UserManagementRepository.UserManagementRepository(context, CreateMapper());

        Assert.False(await repository.HasActiveMembership("u1", 5));
    }

    [Fact]
    public async Task HasActiveMembership_DifferentApplication_ReturnsFalse()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.UserInApplications.Add(new UserInApplication { UserId = "u1", ApplicationId = 5, IsActive = true });
        context.SaveChanges();

        var repository = new Infrastructure.UserManagementRepository.UserManagementRepository(context, CreateMapper());

        Assert.False(await repository.HasActiveMembership("u1", 6));
    }
}
