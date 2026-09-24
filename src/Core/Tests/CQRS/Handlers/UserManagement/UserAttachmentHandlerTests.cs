using Application.Contracts.UserManagement;
using Application.CQRS.Command.UserManagement;
using Application.CQRS.Handlers.UserManagement;
using Application.CQRS.Queries.UserManagement;
using Application.Mapper;
using Application.UnitOfWork;
using Application.UserManagementRepository;
using AutoMapper;
using Domains.Entities.User;
using Infrastructure.Mapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Core.Tests.CQRS.Handlers.UserManagement;

public class CreateUserAttachmentHandlerTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    [Fact]
    public async Task Handle_TargetUserDoesNotExist_ThrowsAndNeverCreatesAttachment()
    {
        var repository = new Mock<IUserAttachmentRepository>();
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.UserExists("ghost", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new CreateUserAttachmentHandler(repository.Object, userManagementRepository.Object, CreateMapper(), new Mock<IUnitOfWork>().Object);

        var command = new CreateUserAttachmentCommand(new UserAttachmentDto { UserId = "ghost", Title = "Doc", AttachmentType = 1 });

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.Handle(command, CancellationToken.None));

        repository.Verify(r => r.Create(It.IsAny<UserAttachment>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TargetUserExists_CreatesAttachmentThroughTypedRepositoryAndSaves()
    {
        var repository = new Mock<IUserAttachmentRepository>();
        repository.Setup(r => r.Create(It.IsAny<UserAttachment>())).ReturnsAsync((UserAttachment a) => a);
        var userManagementRepository = new Mock<IUserManagementRepository>();
        userManagementRepository.Setup(r => r.UserExists("u1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new CreateUserAttachmentHandler(repository.Object, userManagementRepository.Object, CreateMapper(), unitOfWork.Object);

        var command = new CreateUserAttachmentCommand(new UserAttachmentDto { UserId = "u1", Title = "Doc", AttachmentType = 1 });

        await sut.Handle(command, CancellationToken.None);

        repository.Verify(r => r.Create(It.Is<UserAttachment>(a => a.UserId == "u1")), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetUserAttachmentByIdHandlerTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    [Fact]
    public async Task Handle_DelegatesToOwnerScopedRepositoryLookup()
    {
        var repository = new Mock<IUserAttachmentRepository>();
        repository.Setup(r => r.GetByIdForUser(5, "u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAttachment { Id = 5, UserId = "u1", Title = "Doc", AttachmentType = 1 });

        var sut = new GetUserAttachmentByIdHandler(repository.Object, CreateMapper());

        var result = await sut.Handle(new GetUserAttachmentByIdQuery(5, "u1"), CancellationToken.None);

        Assert.Equal("Doc", result.Title);
    }

    [Fact]
    public async Task Handle_CrossUserLookup_PropagatesNotFound()
    {
        var repository = new Mock<IUserAttachmentRepository>();
        repository.Setup(r => r.GetByIdForUser(5, "attacker", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var sut = new GetUserAttachmentByIdHandler(repository.Object, CreateMapper());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.Handle(new GetUserAttachmentByIdQuery(5, "attacker"), CancellationToken.None));
    }
}
