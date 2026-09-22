using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Domains.Entities.User;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Web.Tests;

// Regression coverage for the UserAttachment BackOffice workflow's ownership/authorization
// hardening: SuperAdmin-only, antiforgery-protected, and UploadAttachmentFile must reject a
// mismatched attachmentId/userId pair before ever touching the filesystem.
public sealed class UserAttachmentTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UserAttachmentTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SeedAttachmentAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attachment = new UserAttachment { UserId = userId, Title = "Doc", AttachmentType = 1 };
        context.UserAttachments.Add(attachment);
        await context.SaveChangesAsync();
        return attachment.Id;
    }

    private string StoragePathFor(string userId)
    {
        var env = _factory.Services.GetRequiredService<IHostEnvironment>();
        return Path.Combine(env.ContentRootPath, "wwwroot", "Storage", "UserAttachment", userId);
    }

    [Fact]
    public async Task Post_SaveUserAttachment_WithoutAntiforgeryToken_IsRejected()
    {
        var email = $"attach-noaf-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        var response = await client.PostAsync("/BackOffice/UserAttachment/SaveUserAttachment", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = "victim",
            ["Title"] = "Doc",
            ["AttachmentType"] = "1"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_SaveUserAttachment_AsOrdinaryMember_IsForbidden()
    {
        var email = $"attach-ordinary-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var response = await client.PostAsync("/BackOffice/UserAttachment/SaveUserAttachment", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = "victim",
            ["Title"] = "Doc",
            ["AttachmentType"] = "1"
        }));

        // Cookie auth redirects an authenticated-but-unauthorized request to the access-denied
        // path rather than returning a raw 403; the redirect (not a 200/"Done") is what proves
        // the role gate held.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAttachmentFile_MismatchedOwnership_RejectsBeforeWritingFile()
    {
        var email = $"attach-mismatch-{Guid.NewGuid():N}@test.local";
        var superAdmin = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, superAdmin);

        var victimUserId = $"victim-{Guid.NewGuid():N}";
        var attackerUserId = $"attacker-{Guid.NewGuid():N}";
        var victimAttachmentId = await SeedAttachmentAsync(victimUserId);
        var attackerStoragePath = StoragePathFor(attackerUserId);

        try
        {
            using var content = new MultipartFormDataContent
            {
                { new StringContent(attackerUserId), "userId" },
                { new StringContent(victimAttachmentId.ToString()), "attachmentId" },
                { new StringContent("passport"), "attachmentType" },
                { new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }), "file", "doc.pdf" }
            };

            var response = await client.PostAsync("/BackOffice/UserAttachment/UploadAttachmentFile", content);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(Directory.Exists(attackerStoragePath));
        }
        finally
        {
            if (Directory.Exists(attackerStoragePath))
                Directory.Delete(attackerStoragePath, recursive: true);
        }
    }

    [Fact]
    public async Task UploadAttachmentFile_MatchingOwnership_WritesFile()
    {
        var email = $"attach-match-{Guid.NewGuid():N}@test.local";
        var superAdmin = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, superAdmin);

        var ownerUserId = $"owner-{Guid.NewGuid():N}";
        var attachmentId = await SeedAttachmentAsync(ownerUserId);
        var ownerStoragePath = StoragePathFor(ownerUserId);

        try
        {
            using var content = new MultipartFormDataContent
            {
                { new StringContent(ownerUserId), "userId" },
                { new StringContent(attachmentId.ToString()), "attachmentId" },
                { new StringContent("passport"), "attachmentType" },
                { new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }), "file", "doc.pdf" }
            };

            var response = await client.PostAsync("/BackOffice/UserAttachment/UploadAttachmentFile", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(Directory.Exists(ownerStoragePath));
            Assert.Single(Directory.GetFiles(ownerStoragePath));
        }
        finally
        {
            if (Directory.Exists(ownerStoragePath))
                Directory.Delete(ownerStoragePath, recursive: true);
        }
    }
}
