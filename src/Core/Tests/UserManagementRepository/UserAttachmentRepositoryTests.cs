using System.Collections.Generic;
using Core.Tests.TestSupport;
using Domains.Entities.User;
using Infrastructure.UserManagementRepository;
using Xunit;

namespace Core.Tests.UserManagementRepository;

public class UserAttachmentRepositoryTests
{
    [Fact]
    public async Task GetByIdForUser_OwnerMatch_ReturnsAttachment()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.UserAttachments.Add(new UserAttachment { UserId = "u1", Title = "Passport", AttachmentType = 1 });
        context.SaveChanges();
        var id = context.UserAttachments.Single().Id;

        var repository = new UserAttachmentRepository(context);

        var attachment = await repository.GetByIdForUser(id, "u1");

        Assert.Equal("Passport", attachment.Title);
    }

    [Fact]
    public async Task GetByIdForUser_CrossUserAttachmentId_ThrowsKeyNotFound()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        context.UserAttachments.Add(new UserAttachment { UserId = "victim", Title = "Passport", AttachmentType = 1 });
        context.SaveChanges();
        var id = context.UserAttachments.Single().Id;

        var repository = new UserAttachmentRepository(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetByIdForUser(id, "attacker"));
    }

    [Fact]
    public async Task GetByIdForUser_DeletedAttachment_ThrowsKeyNotFound()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var attachment = new UserAttachment { UserId = "u1", Title = "Passport", AttachmentType = 1 };
        context.UserAttachments.Add(attachment);
        context.SaveChanges();
        var id = attachment.Id;

        context.UserAttachments.Remove(attachment);
        context.SaveChanges();

        var repository = new UserAttachmentRepository(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetByIdForUser(id, "u1"));
    }

    [Fact]
    public async Task GetByIdForUser_NonexistentAttachmentId_ThrowsKeyNotFound()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var repository = new UserAttachmentRepository(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.GetByIdForUser(999, "u1"));
    }
}
