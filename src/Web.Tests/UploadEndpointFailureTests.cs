using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Domains.Entities.ContentManagement;
using Domains.Entities.CustomModule;
using Domains.Entities.General;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SkiaSharp;
using Web.Authorization;
using Xunit;

namespace Web.Tests;

// Web Phase 5 task 2: representative HTTP coverage that upload endpoint failures are predictable
// (a deliberate client-compatible response, never a 500 or a silent partial success) while valid
// input still preserves the existing success contract. UserAttachmentTests already covers
// UserAttachmentController's ownership-before-filesystem-work case; this file covers Content,
// Schema, and Slider.
public sealed class UploadEndpointFailureTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UploadEndpointFailureTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static byte[] ValidPngBytes()
    {
        using var bitmap = new SKBitmap(20, 20);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static byte[] InvalidImageBytes() => Encoding.UTF8.GetBytes("not an image");

    private string StorageDir(params string[] segments)
    {
        var env = _factory.Services.GetRequiredService<IHostEnvironment>();
        return Path.Combine(new[] { env.ContentRootPath, "wwwroot", "Storage" }.Concat(segments).ToArray());
    }

    private async Task<(HttpClient Client, ApplicationUser User, int ApplicationId)> AuthenticatedClientAsync(string prefix, string accessKey)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, accessKey);
        return (client, user, applicationId);
    }

    private static void DeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }

    // ---- ContentController: UploadBodyImage ----

    [Fact]
    public async Task UploadBodyImage_InvalidImage_ReturnsFailed_WithoutWritingFile()
    {
        var (client, _, _) = await AuthenticatedClientAsync("body-img-invalid", AccessKeys.Content.SaveBody);
        var dir = StorageDir("Section", "Images");
        var before = Directory.Exists(dir) ? Directory.GetFiles(dir).ToHashSet() : new HashSet<string>();

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(InvalidImageBytes()), "File", "fake.png" }
        };
        var response = await client.PostAsync("/BackOffice/Content/UploadBodyImage", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", body);
        var after = Directory.Exists(dir) ? Directory.GetFiles(dir).ToHashSet() : new HashSet<string>();
        Assert.Empty(after.Except(before));
    }

    [Fact]
    public async Task UploadBodyImage_ValidImage_ReturnsDoneWithFileName_AndWritesFile()
    {
        var (client, _, _) = await AuthenticatedClientAsync("body-img-valid", AccessKeys.Content.SaveBody);
        var dir = StorageDir("Section", "Images");

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(ValidPngBytes()), "File", "img.png" }
        };
        var response = await client.PostAsync("/BackOffice/Content/UploadBodyImage", content);
        var body = await response.Content.ReadAsStringAsync();

        try
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("Done|", body);
            var fileName = body.Split('|')[1];
            Assert.True(File.Exists(Path.Combine(dir, fileName)));
        }
        finally
        {
            if (body.StartsWith("Done|"))
                DeleteIfExists(Path.Combine(dir, body.Split('|')[1]));
        }
    }

    // ---- ContentController: UploadBodyImageGallery ----

    [Fact]
    public async Task UploadBodyImageGallery_EmptySelection_ReturnsFailed()
    {
        var (client, _, _) = await AuthenticatedClientAsync("gallery-empty", AccessKeys.Content.SaveBody);

        // A genuinely zero-part multipart body (no fields at all) never reaches the action - the
        // antiforgery filter's own form read on it is a separate, pre-existing framework behavior
        // this task doesn't touch ("keep antiforgery behavior intact"). A non-file field keeps the
        // body realistic (a real FormData post always carries the antiforgery-adjacent request
        // machinery) while still exercising Request.Form.Files.Count == 0.
        using var content = new MultipartFormDataContent
        {
            { new StringContent("x"), "dummy" }
        };
        var response = await client.PostAsync("/BackOffice/Content/UploadBodyImageGallery", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", body);
    }

    [Fact]
    public async Task UploadBodyImageGallery_AllValid_ReturnsDoneWithEveryFileName_AndWritesAllFiles()
    {
        var (client, _, _) = await AuthenticatedClientAsync("gallery-valid", AccessKeys.Content.SaveBody);
        var dir = StorageDir("Section", "Gallery");

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(ValidPngBytes()), "img0", "a.png" },
            { new ByteArrayContent(ValidPngBytes()), "img1", "b.png" }
        };
        var response = await client.PostAsync("/BackOffice/Content/UploadBodyImageGallery", content);
        var body = await response.Content.ReadAsStringAsync();

        try
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("Done|", body);
            var fileNames = body.Substring("Done|".Length).Split(',', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, fileNames.Length);
            foreach (var fileName in fileNames)
                Assert.True(File.Exists(Path.Combine(dir, fileName)));
        }
        finally
        {
            if (body.StartsWith("Done|"))
                foreach (var fileName in body.Substring("Done|".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    DeleteIfExists(Path.Combine(dir, fileName));
        }
    }

    [Fact]
    public async Task UploadBodyImageGallery_OneInvalidFile_RejectsWholeBatch_AndWritesNoFiles()
    {
        var (client, _, _) = await AuthenticatedClientAsync("gallery-partial", AccessKeys.Content.SaveBody);
        var dir = StorageDir("Section", "Gallery");
        var before = Directory.Exists(dir) ? Directory.GetFiles(dir).ToHashSet() : new HashSet<string>();

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(ValidPngBytes()), "img0", "a.png" },
            { new ByteArrayContent(InvalidImageBytes()), "img1", "b.png" }
        };
        var response = await client.PostAsync("/BackOffice/Content/UploadBodyImageGallery", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", body);
        var after = Directory.Exists(dir) ? Directory.GetFiles(dir).ToHashSet() : new HashSet<string>();
        Assert.Empty(after.Except(before));
    }

    // ---- ContentController: UploadContentImage ----

    private async Task<int> SeedContentAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contentEntity = new Content { ApplicationId = applicationId, TypeId = 1000, Title = "Upload target", PublishDt = DateTime.UtcNow };
        context.Contents.Add(contentEntity);
        await context.SaveChangesAsync();
        return contentEntity.Id;
    }

    private async Task<int> SeedImageSizeSettingAsync(int applicationId, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var setting = new ApplicationSetting { ApplicationId = applicationId, SettingId = 1000, Title = "Size", Value = value };
        context.ApplicationSettings.Add(setting);
        await context.SaveChangesAsync();
        return setting.Id;
    }

    private async Task<int> ContentImageCountAsync(int contentId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.ContentImages.CountAsync(ci => ci.ContentId == contentId);
    }

    private async Task<int> SeedContentImageAsync(int contentId, string imageFileName)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contentImage = new ContentImage { ContentId = contentId, ImageFileName = imageFileName, IsActive = true, Size = 50 };
        context.ContentImages.Add(contentImage);
        await context.SaveChangesAsync();
        return contentImage.Id;
    }

    private async Task<bool> ContentImageExistsAsync(int contentImageId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.ContentImages.AnyAsync(ci => ci.Id == contentImageId);
    }

    [Fact]
    public async Task UploadContentImage_InvalidSettingId_ReturnsFailed_WithoutCreatingContentImage()
    {
        var (client, _, applicationId) = await AuthenticatedClientAsync("content-img-badsetting", AccessKeys.Content.UploadImages);
        var contentId = await SeedContentAsync(applicationId);

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(ValidPngBytes()), "File", "img.png" },
            { new StringContent(contentId.ToString()), "ContentId" },
            { new StringContent("999999"), "SettingId" }
        };
        var response = await client.PostAsync("/BackOffice/Content/UploadContentImage", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", body);
        Assert.Equal(0, await ContentImageCountAsync(contentId));
    }

    [Fact]
    public async Task UploadContentImage_CrossTenantContentId_ReturnsFailed_WithoutTouchingOtherTenantFilesOrRecords()
    {
        var (clientA, _, applicationIdA) = await AuthenticatedClientAsync("content-img-tenant-a", AccessKeys.Content.UploadImages);
        var (_, _, applicationIdB) = await AuthenticatedClientAsync("content-img-tenant-b", AccessKeys.Content.UploadImages);

        // A valid, same-tenant (A) setting id, so the request would otherwise fully succeed - the
        // only thing that must reject it is that contentId belongs to tenant B, not A.
        var settingIdA = await SeedImageSizeSettingAsync(applicationIdA, "50-50");

        // Tenant B's content, with a pre-existing image file and DB record that a cross-tenant
        // attacker submitting B's contentId must not be able to touch.
        var contentIdB = await SeedContentAsync(applicationIdB);
        var dirB = StorageDir("Content", "Image", contentIdB.ToString());
        Directory.CreateDirectory(dirB);
        var sentinelFile = Path.Combine(dirB, "sentinel.png");
        await File.WriteAllBytesAsync(sentinelFile, ValidPngBytes());
        var existingContentImageId = await SeedContentImageAsync(contentIdB, "sentinel.png");

        try
        {
            using var content = new MultipartFormDataContent
            {
                { new ByteArrayContent(ValidPngBytes()), "File", "attack.png" },
                { new StringContent(contentIdB.ToString()), "ContentId" },
                { new StringContent(settingIdA.ToString()), "SettingId" }
            };
            // clientA is authenticated and holds Content.UploadImages, but only within tenant A -
            // it submits tenant B's contentId directly.
            var response = await clientA.PostAsync("/BackOffice/Content/UploadContentImage", content);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Failed", body);

            Assert.True(File.Exists(sentinelFile));
            Assert.Single(Directory.GetFiles(dirB));
            Assert.Equal(1, await ContentImageCountAsync(contentIdB));
            Assert.True(await ContentImageExistsAsync(existingContentImageId));
        }
        finally
        {
            if (Directory.Exists(dirB))
                Directory.Delete(dirB, recursive: true);
        }
    }

    [Fact]
    public async Task UploadContentImage_ValidSetting_CreatesContentImagesAndWritesFiles()
    {
        var (client, _, applicationId) = await AuthenticatedClientAsync("content-img-valid", AccessKeys.Content.UploadImages);
        var contentId = await SeedContentAsync(applicationId);
        var settingId = await SeedImageSizeSettingAsync(applicationId, "50-50");
        var dir = StorageDir("Content", "Image", contentId.ToString());

        try
        {
            using var content = new MultipartFormDataContent
            {
                { new ByteArrayContent(ValidPngBytes()), "File", "img.png" },
                { new StringContent(contentId.ToString()), "ContentId" },
                { new StringContent(settingId.ToString()), "SettingId" }
            };
            var response = await client.PostAsync("/BackOffice/Content/UploadContentImage", content);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("Done", body);
            Assert.Equal(1, await ContentImageCountAsync(contentId));
            Assert.True(Directory.Exists(dir));
            Assert.Single(Directory.GetFiles(dir));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    // ---- SchemaController: UploadSchemaLogo ----

    private async Task<int> SeedSchemaAsync(int applicationId, string logoFileName)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schema = new Schema { ApplicationId = applicationId, Title = "Upload target", LogoFileName = logoFileName, TypeId = 1000 };
        context.Schemas.Add(schema);
        await context.SaveChangesAsync();
        return schema.Id;
    }

    [Fact]
    public async Task UploadSchemaLogo_UnknownEntityId_ReturnsFailed_WithoutThrowing()
    {
        var (client, _, _) = await AuthenticatedClientAsync("schema-logo-unknown", AccessKeys.Schema.Module);

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(ValidPngBytes()), "File", "logo.png" },
            { new StringContent("999999"), "EntityId" }
        };
        var response = await client.PostAsync("/BackOffice/Schema/UploadSchemaLogo", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", body);
    }

    [Fact]
    public async Task UploadSchemaLogo_ValidEntityId_ReturnsDone_AndWritesFile()
    {
        var (client, _, applicationId) = await AuthenticatedClientAsync("schema-logo-valid", AccessKeys.Schema.Module);
        var logoBaseName = Guid.NewGuid().ToString();
        var schemaId = await SeedSchemaAsync(applicationId, logoBaseName + ".png");
        var dir = StorageDir("Schema", "Logos");
        var expectedFile = Path.Combine(dir, logoBaseName + ".png");

        try
        {
            using var content = new MultipartFormDataContent
            {
                { new ByteArrayContent(ValidPngBytes()), "File", "logo.png" },
                { new StringContent(schemaId.ToString()), "EntityId" }
            };
            var response = await client.PostAsync("/BackOffice/Schema/UploadSchemaLogo", content);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Done", body);
            Assert.True(File.Exists(expectedFile));
        }
        finally
        {
            DeleteIfExists(expectedFile);
        }
    }

    // ---- SliderController: UploadSliderItemImage ----

    private async Task<int> SeedSliderAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slider = new Slider { ApplicationId = applicationId, Title = "Upload target" };
        context.Sliders.Add(slider);
        await context.SaveChangesAsync();
        return slider.Id;
    }

    [Fact]
    public async Task UploadSliderItemImage_InvalidImage_ReturnsBadRequest_WithoutWritingFile()
    {
        var (client, _, applicationId) = await AuthenticatedClientAsync("slider-img-invalid", AccessKeys.Slider.SaveItem);
        var sliderId = await SeedSliderAsync(applicationId);
        var dir = StorageDir("Slider", sliderId.ToString());

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(InvalidImageBytes()), "file", "fake.jpg" },
            { new StringContent(sliderId.ToString()), "sliderId" },
            { new StringContent(Guid.NewGuid().ToString()), "imageFileName" }
        };
        var response = await client.PostAsync("/BackOffice/Slider/UploadSliderItemImage", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(Directory.Exists(dir) && Directory.GetFiles(dir).Any());
    }

    [Fact]
    public async Task UploadSliderItemImage_ValidImage_ReturnsOkWithFileName_AndWritesFile()
    {
        var (client, _, applicationId) = await AuthenticatedClientAsync("slider-img-valid", AccessKeys.Slider.SaveItem);
        var sliderId = await SeedSliderAsync(applicationId);
        var dir = StorageDir("Slider", sliderId.ToString());

        try
        {
            using var content = new MultipartFormDataContent
            {
                { new ByteArrayContent(ValidPngBytes()), "file", "img.jpg" },
                { new StringContent(sliderId.ToString()), "sliderId" },
                { new StringContent(Guid.NewGuid().ToString()), "imageFileName" }
            };
            var response = await client.PostAsync("/BackOffice/Slider/UploadSliderItemImage", content);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(File.Exists(Path.Combine(dir, body)));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
