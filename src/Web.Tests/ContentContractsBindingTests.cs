using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Domains.Entities.ContentManagement;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Web.Tests;

// Pins the exact field names and nesting the ContentController actions bind, now that the five
// CMS input models moved from Web.Models.CMS into
// Web.Areas.BackOffice.Features.Content.Contracts. A rename or shape drift on any of these
// contracts would surface here as a binding failure or a value that doesn't round-trip, not as a
// compile error.
public sealed class ContentContractsBindingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContentContractsBindingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, int ApplicationId)> AuthenticatedTenantClientAsync(string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedAdminUserAsync(_factory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(_factory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        return (client, applicationId);
    }

    private async Task<Content> SeedContentWithFarsiShapeAsync(int applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var content = new Content
        {
            ApplicationId = applicationId,
            TypeId = 1000,
            Title = "English title",
            HeadLine = "English headline",
            Abstract = "English abstract",
            Description = "English description",
            PublishDt = DateTime.UtcNow
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();

        context.ContentMetadatas.Add(new ContentMetadata { ContentId = content.Id, Title = "Meta title" });
        var section = new ContentSection { ContentId = content.Id, Priority = 1 };
        context.ContentSections.Add(section);
        await context.SaveChangesAsync();

        var element = new SectionElement { SectionId = section.Id, ElementType = 1000, TinyText = "original tiny text" };
        context.SectionElements.Add(element);
        await context.SaveChangesAsync();

        return await context.Contents
            .Include(c => c.Metadata)
            .Include(c => c.Sections).ThenInclude(s => s.Elements)
            .SingleAsync(c => c.Id == content.Id);
    }

    // FarsiContentEditDto/FarsiContentMetadataEditDto/FarsiSectionEditDto/FarsiSectionElementEditDto
    // are bound from the standard ASP.NET Core form-encoded indexer convention the Razor form
    // emits (Sections[0].SectionElements[0].TinyText, Metadata.Title, ...). This proves that shape
    // still binds correctly onto the moved, feature-owned contracts.
    [Fact]
    public async Task SaveFarsiContentForm_BindsIndexedFormFields_IntoMovedContracts()
    {
        var (client, applicationId) = await AuthenticatedTenantClientAsync("farsi-bind");
        var content = await SeedContentWithFarsiShapeAsync(applicationId);
        var section = content.Sections.Single();
        var element = section.Elements.Single();

        var form = new Dictionary<string, string>
        {
            ["Id"] = content.Id.ToString(),
            ["Title"] = "Updated Farsi title",
            ["HeadLine"] = "Updated Farsi headline",
            ["Abstract"] = "Updated Farsi abstract",
            ["Description"] = "Updated Farsi description",
            ["Metadata.Title"] = "Updated meta title",
            ["Metadata.Author"] = "Updated author",
            ["Metadata.Keywords"] = "kw1,kw2",
            ["Metadata.Description"] = "Updated meta description",
            [$"Sections[0].Id"] = section.Id.ToString(),
            [$"Sections[0].SectionElements[0].Id"] = element.Id.ToString(),
            [$"Sections[0].SectionElements[0].TinyText"] = "updated tiny text",
        };

        var response = await client.PostAsync(
            "/BackOffice/Content/SaveFarsiContentForm",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Done", await response.Content.ReadAsStringAsync());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verifyContext.Contents.SingleAsync(c => c.Id == content.Id);
        var farsi = JObject.Parse(saved.FarsiContent);

        Assert.Equal("Updated Farsi title", farsi.Value<string>("Title"));
        Assert.Equal("Updated meta title", farsi["Metadata"]!.Value<string>("Title"));
        Assert.Equal("updated tiny text", farsi["Sections"]![0]!["Elements"]![0]!.Value<string>("TinyText"));
    }

    // SaveSection binds SaveContentBodyDto (and its nested ContentBodyElementDto list) from a raw
    // JSON body, matching the JS client's `contentType: "application/json"` + JSON.stringify call.
    // This proves that JSON shape still binds onto the moved contract.
    [Fact]
    public async Task SaveSection_BindsJsonBody_IntoMovedContract()
    {
        var (client, applicationId) = await AuthenticatedTenantClientAsync("section-bind");
        var content = await SeedContentWithFarsiShapeAsync(applicationId);
        var section = content.Sections.Single();
        var element = section.Elements.Single();

        var payload = new
        {
            SectionId = section.Id,
            ContentId = content.Id,
            Priority = section.Priority,
            Elements = new[]
            {
                new
                {
                    Id = element.Id,
                    ElementType = 1000,
                    Value = "json bound tiny text",
                    ContentId = content.Id,
                    Size = 0,
                    Title = element.ElementTitle
                }
            }
        };

        var response = await client.PostAsync(
            "/BackOffice/Content/SaveSection",
            new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Done", await response.Content.ReadAsStringAsync());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedElement = await verifyContext.SectionElements.SingleAsync(e => e.Id == element.Id);

        Assert.Equal("json bound tiny text", savedElement.TinyText);
    }
}
