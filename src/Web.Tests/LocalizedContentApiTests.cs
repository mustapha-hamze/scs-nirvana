using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// GET api/Content/GetLocalizedContent/{applicationId}/{id}?culture=: rollout gates, culture
// validation, tenant isolation, resolution over HTTP, and the legacy GetContent route unchanged.
public sealed class LocalizedContentApiTests : IClassFixture<TestWebApplicationFactory>
{
    private const int ApplicationId = 9101;
    private const int OtherApplicationId = 9102;
    private const int NotEnabledApplicationId = 9103;
    private const int FarsiCultureId = 7101;
    private const int GermanCultureId = 7102;
    private const string SecretError = "provider-secret-error";

    private readonly TestWebApplicationFactory _baseFactory;
    private readonly WebApplicationFactory<Program> _factory;

    public LocalizedContentApiTests(TestWebApplicationFactory factory)
    {
        _baseFactory = factory;
        _factory = Configure(factory, enabled: true);
    }

    private static WebApplicationFactory<Program> Configure(WebApplicationFactory<Program> factory, bool enabled) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ContentTranslation:LocalizedReadEnabled", enabled.ToString());
            builder.UseSetting("ContentTranslation:LocalizedReadApplicationIds:0", ApplicationId.ToString());
            builder.UseSetting("ContentTranslation:LocalizedReadApplicationIds:1", OtherApplicationId.ToString());
            builder.UseSetting("ContentTranslation:LegacyFarsiCultureId", FarsiCultureId.ToString());
        });

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    // Returns (contentId, sectionId, elementId, metadataId).
    private Task<(int, int, int, int)> SeedContent(int applicationId = ApplicationId, bool legacyFarsi = true, bool readyTranslation = false) => Db(async context =>
    {
        if (!await context.Cultures.IgnoreQueryFilters().AnyAsync(c => c.Id == FarsiCultureId))
        {
            context.Cultures.AddRange(
                new Culture { Id = FarsiCultureId, ApplicationId = ApplicationId, Title = "Farsi", Key = "fa-IR", IsActive = true },
                new Culture { Id = GermanCultureId, ApplicationId = ApplicationId, Title = "German", Key = "de-DE", IsActive = true });
        }

        var content = new Content
        {
            ApplicationId = applicationId, TypeId = 1000, Title = "English", PublishDt = new DateTime(2026, 1, 1), IsActive = true,
            Metadata = new ContentMetadata { Title = "Meta" },
            Images = new List<ContentImage> { new() { ImageFileName = "img.jpg" } },
            Sections = new List<ContentSection>
            {
                new() { Priority = 1, Elements = new List<SectionElement> { new() { ElementType = 1000, TinyText = "tiny", FileNameText = "file.pdf" } } }
            }
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        var section = content.Sections.Single();
        var element = section.Elements.Single();
        if (legacyFarsi)
            content.FarsiContent = $"{{\"Id\":{content.Id},\"Title\":\"قدیمی\",\"Sections\":[{{\"Id\":{section.Id},\"Elements\":[{{\"Id\":{element.Id},\"TinyText\":\"قدیم\"}}]}}]}}";
        if (readyTranslation)
        {
            context.ContentTranslations.Add(new ContentTranslation
            {
                ContentId = content.Id, CultureId = FarsiCultureId, TranslationStatus = TranslationStatus.Ready,
                SourceFingerprint = ContentSourceFingerprint.Compute(content),
                LocalizedTextJson = LegacyFarsiContentParser.Serialize(new LocalizedContentText("عنوان", null, null, null,
                    new LocalizedMetadataText(content.Metadata.Id, "متا", null, null, null),
                    [new LocalizedSectionText(section.Id, [new LocalizedElementText(element.Id, "کوچک", null)])])),
                Provider = "openai", Model = "secret-model", Error = SecretError, IsActive = true
            });
        }

        await context.SaveChangesAsync();
        return (content.Id, section.Id, element.Id, content.Metadata.Id);
    });

    private static async Task<(HttpStatusCode Status, string Body)> Get(WebApplicationFactory<Program> factory, string path)
    {
        var response = await factory.CreateClient().GetAsync(path);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private Task<(HttpStatusCode Status, string Body)> GetLocalized(int contentId, string query, int applicationId = ApplicationId) =>
        Get(_factory, $"/api/Content/GetLocalizedContent/{applicationId}/{contentId}{query}");

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    [Fact]
    public async Task ReadyTranslation_IsServed_WithoutLegacyOrProviderData()
    {
        var (contentId, _, _, _) = await SeedContent(readyTranslation: true);

        var (status, body) = await GetLocalized(contentId, "?culture=FA-ir");

        Assert.Equal(HttpStatusCode.OK, status);
        var json = Json(body);
        Assert.Equal("عنوان", json.GetProperty("title").GetString());
        Assert.Equal("متا", json.GetProperty("metadata").GetProperty("title").GetString());
        var element = json.GetProperty("sections")[0].GetProperty("elements")[0];
        Assert.Equal("کوچک", element.GetProperty("tinyText").GetString());
        Assert.Equal("file.pdf", element.GetProperty("fileNameText").GetString());
        Assert.Equal("img.jpg", json.GetProperty("images")[0].GetProperty("imageFileName").GetString());
        Assert.Equal("fa-IR", json.GetProperty("culture").GetString());
        Assert.Equal("translation", json.GetProperty("resolution").GetString());
        foreach (var leaked in new[] { "farsiContent", "provider", "model", "error", "localizedTextJson", "sourceFingerprint" })
            Assert.False(json.TryGetProperty(leaked, out _), leaked);
        Assert.DoesNotContain(SecretError, body);
        Assert.DoesNotContain("secret-model", body);
        Assert.DoesNotContain("قدیمی", body);
    }

    [Fact]
    public async Task LegacyFarsi_IsServedOnlyForTheLegacyCulture()
    {
        var (contentId, _, _, _) = await SeedContent();

        var farsi = Json((await GetLocalized(contentId, "?culture=fa-IR")).Body);
        Assert.Equal("legacy-farsi", farsi.GetProperty("resolution").GetString());
        Assert.Equal("قدیمی", farsi.GetProperty("title").GetString());

        var (status, body) = await GetLocalized(contentId, "?culture=de-DE");
        Assert.Equal(HttpStatusCode.OK, status);
        var german = Json(body);
        Assert.Equal("source", german.GetProperty("resolution").GetString());
        Assert.Equal("de-DE", german.GetProperty("culture").GetString());
        Assert.Equal("English", german.GetProperty("title").GetString());
        Assert.DoesNotContain("قدیم", body);
    }

    [Fact]
    public async Task UnknownCulture_FallsBackToEnglish()
    {
        var (contentId, _, _, _) = await SeedContent(legacyFarsi: false);

        var json = Json((await GetLocalized(contentId, "?culture=ja-JP")).Body);

        Assert.Equal("source", json.GetProperty("resolution").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("culture").ValueKind);
        Assert.Equal("English", json.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?culture=")]
    [InlineData("?culture=fa_IR")]
    [InlineData("?culture=%3Cscript%3E")]
    public async Task InvalidCulture_Returns400(string query)
    {
        var (contentId, _, _, _) = await SeedContent();

        var (status, body) = await GetLocalized(contentId, query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.DoesNotContain("English", body);
    }

    [Fact]
    public async Task OtherApplications_CannotReadTheContent()
    {
        var (contentId, _, _, _) = await SeedContent(readyTranslation: true);

        Assert.Equal(HttpStatusCode.NotFound, (await GetLocalized(contentId, "?culture=fa-IR", OtherApplicationId)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await GetLocalized(contentId + 100000, "?culture=fa-IR")).Status);
    }

    [Fact]
    public async Task RolloutGates_ReturnNotFound_WhenClosed()
    {
        var (contentId, _, _, _) = await SeedContent(readyTranslation: true);
        var (notEnabledContentId, _, _, _) = await SeedContent(NotEnabledApplicationId);

        Assert.Equal(HttpStatusCode.NotFound, (await GetLocalized(notEnabledContentId, "?culture=fa-IR", NotEnabledApplicationId)).Status);

        // Defaults (nothing configured): off for the environment.
        Assert.Equal(HttpStatusCode.NotFound, (await Get(_baseFactory, $"/api/Content/GetLocalizedContent/{ApplicationId}/{contentId}?culture=fa-IR")).Status);

        var disabled = Configure(_baseFactory, enabled: false);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(disabled, $"/api/Content/GetLocalizedContent/{ApplicationId}/{contentId}?culture=fa-IR")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(disabled, $"/api/Content/GetLocalizedContent/{ApplicationId}/{contentId}?culture=bad_value")).Status);
    }

    [Fact]
    public async Task LegacyGetContent_IsUnchanged_ByTheNewContractOrItsFlags()
    {
        var (contentId, _, _, _) = await SeedContent(readyTranslation: true);
        var path = $"/api/Content/GetContent/{ApplicationId}/{contentId}";

        var (status, enabledBody) = await Get(_factory, path);
        var (_, disabledBody) = await Get(Configure(_baseFactory, enabled: false), path);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(disabledBody, enabledBody);
        var json = Json(enabledBody);
        Assert.Equal(
            new[] { "id", "status", "isDeleted", "isActive", "updatedDT", "createdDT", "applicationId", "typeId", "title", "headLine", "abstract",
                "description", "farsiContent", "categories", "tags", "cultures", "publishDt", "sections", "images", "metadata" }.OrderBy(k => k),
            json.EnumerateObject().Select(p => p.Name).OrderBy(k => k));
        Assert.Equal("English", json.GetProperty("title").GetString());
        Assert.Contains("قدیمی", json.GetProperty("farsiContent").GetString());
        Assert.DoesNotContain("resolution", enabledBody);
    }
}
