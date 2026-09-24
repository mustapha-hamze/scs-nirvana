using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Web.Tests;

// Regression coverage for the ChangeContentActiveMode(mode: true) fix: activating content that
// already has non-empty Farsi content must go through the normal scoped activation path and must
// never call the translation provider or touch the stored Farsi payload. Registers a translator
// double that throws on Translate() - if the fix regresses and the controller re-translates
// existing Farsi content, this test fails loudly instead of flaking on a real OpenAI call.
public sealed class ContentActivationRegressionTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContentActivationRegressionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed class ThrowingContentTranslator : IContentTranslator
    {
        public Task<string> Translate(Content content, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Translation provider must not be invoked for content that already has Farsi content.");
    }

    private WebApplicationFactory<Program> BuildFactoryWithThrowingTranslator()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IContentTranslator>();
                services.AddTransient<IContentTranslator, ThrowingContentTranslator>();
            });
        });
    }

    [Fact]
    public async Task ChangeContentActiveMode_ExistingFarsiContent_ActivatesWithoutTranslating()
    {
        var noTranslateFactory = BuildFactoryWithThrowingTranslator();

        var email = $"content-activate-existing-farsi-{Guid.NewGuid():N}@test.local";
        var user = await AccountFlowHelper.SeedSuperAdminUserAsync(noTranslateFactory, email, "CorrectHorseBattery12");
        var client = await AccountFlowHelper.LoginAsync(noTranslateFactory, email, "CorrectHorseBattery12");
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(noTranslateFactory, client, user);

        const string existingFarsiContent = "{\"Title\":\"عنوان موجود\"}";
        int contentId;
        using (var scope = noTranslateFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var content = new Content
            {
                ApplicationId = applicationId,
                TypeId = 1000,
                Title = "Existing Farsi content",
                PublishDt = DateTime.UtcNow,
                IsActive = false,
                FarsiContent = existingFarsiContent
            };
            context.Contents.Add(content);
            await context.SaveChangesAsync();
            contentId = content.Id;
        }

        var response = await client.PostAsync($"/BackOffice/Content/ChangeContentActiveMode/1000/{contentId}/true", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Done", await response.Content.ReadAsStringAsync());

        using (var scope = noTranslateFactory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var content = await context.Contents.FindAsync(contentId);
            Assert.NotNull(content);
            Assert.True(content.IsActive);
            Assert.Equal(existingFarsiContent, content.FarsiContent);
        }
    }
}
