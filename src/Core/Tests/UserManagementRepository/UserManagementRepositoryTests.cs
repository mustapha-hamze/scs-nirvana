using System;
using System.Collections.Generic;
using AutoMapper;
using Core.Tests.TestSupport;
using Domains.Entities.General;
using Infrastructure.Identity;
using Infrastructure.Mapper;
using Application.Mapper;
using Infrastructure.UserManagementRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Core.Tests.UserManagementRepository;

public class UserManagementRepositoryTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); }, NullLoggerFactory.Instance);
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

    [Fact]
    public async Task List_HonorsIsAdminUserParameter_WhenEmailIsEmpty()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.Users.Add(new ApplicationUser { Id = "u1", Email = "admin@x.com", IsAdminUser = true });
        context.Users.Add(new ApplicationUser { Id = "u2", Email = "member@x.com", IsAdminUser = false });
        context.SaveChanges();

        var repository = new Infrastructure.UserManagementRepository.UserManagementRepository(context, CreateMapper());

        var nonAdmins = await repository.List(isAdminUser: false);
        Assert.Single(nonAdmins);
        Assert.Equal("member@x.com", nonAdmins[0].Email);

        var admins = await repository.List(isAdminUser: true);
        Assert.Single(admins);
        Assert.Equal("admin@x.com", admins[0].Email);
    }

    [Fact]
    public async Task List_FiltersByEmailAndIsAdminUser_InSql()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.Users.Add(new ApplicationUser { Id = "u1", Email = "admin@x.com", IsAdminUser = true });
        context.Users.Add(new ApplicationUser { Id = "u2", Email = "admin2@x.com", IsAdminUser = false });
        context.SaveChanges();

        var repository = new Infrastructure.UserManagementRepository.UserManagementRepository(context, CreateMapper());

        var result = await repository.List(isAdminUser: true, email: "admin");
        Assert.Single(result);
        Assert.Equal("admin@x.com", result[0].Email);
    }

    [Fact]
    public async Task GetUserAccessesWithAppId_UnknownEmail_ThrowsKeyNotFound()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var repository = new Infrastructure.UserManagementRepository.UserManagementRepository(context, CreateMapper());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetUserAccesses("nobody@x.com", 1));
    }
}
