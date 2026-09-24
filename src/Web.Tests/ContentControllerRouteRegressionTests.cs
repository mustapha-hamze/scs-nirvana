using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Web.Tests;

// Regression coverage for splitting the 814-line ContentController into partial class files
// (Areas/BackOffice/Controllers/Content/ContentController.{Forms,Farsi,Relations,Sections,
// Uploads}.cs). Proves, via real endpoint routing/HTTP behavior rather than reflection, that every
// moved action still resolves on its original route+verb, every unsafe action still requires the
// global antiforgery token, and the inherited BaseController/RequireTenantContextFilter
// tenant-scoping still applies across all five partials.
public sealed class ContentControllerRouteRegressionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContentControllerRouteRegressionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Every action across all five behavior partials, with its original route template and verb.
    public static IEnumerable<object[]> AllContentRoutes => new List<object[]>
    {
        new object[] { HttpMethod.Get, "/BackOffice/Content/Index/1" },                                  // Forms
        new object[] { HttpMethod.Get, "/BackOffice/Content/ContentForm/1/1000" },                       // Forms
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveContentForm" },                         // Forms
        new object[] { HttpMethod.Get, "/BackOffice/Content/ContentList/1" },                            // Forms
        new object[] { HttpMethod.Post, "/BackOffice/Content/ChangeContentActiveMode/1000/1/true" },     // Forms
        new object[] { HttpMethod.Delete, "/BackOffice/Content/DeleteContent/1" },                       // Forms
        new object[] { HttpMethod.Get, "/BackOffice/Content/FarsiContentForm/1/1000" },                  // Farsi
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveFarsiContentForm" },                    // Farsi
        new object[] { HttpMethod.Get, "/BackOffice/Content/ContentRelations/1" },                       // Relations
        new object[] { HttpMethod.Get, "/BackOffice/Content/ContentMetadata/1" },                        // Relations
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveRelation/Category/1" },                 // Relations
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveContentMetadata" },                     // Relations
        new object[] { HttpMethod.Get, "/BackOffice/Content/ContentSections/1/1000" },                   // Sections
        new object[] { HttpMethod.Get, "/BackOffice/Content/CreateContentSection/1/1" },                 // Sections
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveSection" },                             // Sections
        new object[] { HttpMethod.Post, "/BackOffice/Content/UpdateSectionsLayoutOrder" },               // Sections
        new object[] { HttpMethod.Delete, "/BackOffice/Content/DeleteSection/1" },                       // Sections
        new object[] { HttpMethod.Get, "/BackOffice/Content/ContentImages/1" },                          // Uploads
        new object[] { HttpMethod.Post, "/BackOffice/Content/UploadBodyImage" },                         // Uploads
        new object[] { HttpMethod.Post, "/BackOffice/Content/UploadBodyFile" },                          // Uploads
        new object[] { HttpMethod.Post, "/BackOffice/Content/UploadBodyImageGallery" },                  // Uploads
        new object[] { HttpMethod.Post, "/BackOffice/Content/UploadContentImage" },                      // Uploads
    };

    public static IEnumerable<object[]> UnsafeContentRoutes => new List<object[]>
    {
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveContentForm" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/ChangeContentActiveMode/1000/1/true" },
        new object[] { HttpMethod.Delete, "/BackOffice/Content/DeleteContent/1" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveFarsiContentForm" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveRelation/Category/1" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveContentMetadata" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/SaveSection" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/UpdateSectionsLayoutOrder" },
        new object[] { HttpMethod.Delete, "/BackOffice/Content/DeleteSection/1" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/UploadBodyImage" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/UploadBodyFile" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/UploadBodyImageGallery" },
        new object[] { HttpMethod.Post, "/BackOffice/Content/UploadContentImage" },
    };

    // One representative GET action per behavior partial (Forms, Farsi, Relations, Sections,
    // Uploads) - GET isn't antiforgery-gated, so this isolates the tenant-context filter.
    public static IEnumerable<object[]> RepresentativeGetActionsPerPartial => new List<object[]>
    {
        new object[] { "/BackOffice/Content/ContentForm/1/1000" },
        new object[] { "/BackOffice/Content/FarsiContentForm/1/1000" },
        new object[] { "/BackOffice/Content/ContentRelations/1" },
        new object[] { "/BackOffice/Content/ContentSections/1/1000" },
        new object[] { "/BackOffice/Content/ContentImages/1" },
    };

    private static HttpRequestMessage Request(HttpMethod method, string path) => new(method, path);

    [Theory]
    [MemberData(nameof(AllContentRoutes))]
    public async Task AllContentRoutes_UnauthenticatedRequest_RedirectsToLogin(HttpMethod method, string path)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [Theory]
    [MemberData(nameof(UnsafeContentRoutes))]
    public async Task UnsafeContentActions_WithoutAntiforgeryToken_AreRejected(HttpMethod method, string path)
    {
        var email = $"content-noaf-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        var response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RepresentativeGetActionsPerPartial))]
    public async Task ContentActions_AuthenticatedWithoutTenant_RedirectToSelectApp(string path)
    {
        var email = $"content-notenant-{Guid.NewGuid():N}@test.local";
        await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/BackOffice/Application/SelectApp", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    private async Task<int> SeedContentAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var content = new Content { ApplicationId = applicationId, TypeId = 1000, Title = "Regression content", PublishDt = DateTime.UtcNow };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        return content.Id;
    }

    [Fact]
    public async Task ContentList_AuthenticatedWithTenant_ReturnsOk()
    {
        var email = $"content-list-{Guid.NewGuid():N}@test.local";
        // SuperAdmin to bypass the Content access-key gate - this test is about routing, not authorization.
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);

        var response = await client.GetAsync("/BackOffice/Content/ContentList/1000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ContentImages_AuthenticatedWithTenant_ReturnsOk()
    {
        var email = $"content-images-{Guid.NewGuid():N}@test.local";
        // SuperAdmin to bypass the Content access-key gate - this test is about routing, not authorization.
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var contentId = await SeedContentAsync(applicationId);

        // The view indexes ViewData["ContentImageAspectRatio"][0], so the application must have
        // at least one 1001 (aspect ratio) setting - same as a real onboarded application.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.ApplicationSettings.Add(new ApplicationSetting { ApplicationId = applicationId, SettingId = 1001, Title = "AspectRatio", Value = "16:9" });
            await context.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/BackOffice/Content/ContentImages/{contentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SaveContentMetadata_AuthenticatedWithTenant_CreatesAndReturnsDone()
    {
        var email = $"content-metadata-{Guid.NewGuid():N}@test.local";
        // SuperAdmin to bypass the Content access-key gate - this test is about routing, not authorization.
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        var contentId = await SeedContentAsync(applicationId);

        var response = await client.PostAsync("/BackOffice/Content/SaveContentMetadata", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ContentId"] = contentId.ToString(),
            ["Title"] = "Meta title",
            ["Author"] = "Author",
            ["Keywords"] = "kw",
            ["Description"] = "desc"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Done", await response.Content.ReadAsStringAsync());
    }
}
