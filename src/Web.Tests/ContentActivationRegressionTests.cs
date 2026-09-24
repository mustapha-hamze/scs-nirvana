using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Web.Tests;

// ChangeContentActiveMode(mode: true) only activates when the configured activation culture has a
// Ready translation of the current source. It never calls the translation provider (a throwing
// port double is registered), never blocks on translation, and never treats legacy FarsiContent as
// readiness. RequestTranslation queues the background job instead.
public sealed class ContentActivationRegressionTests : IClassFixture<TestWebApplicationFactory>
{
    private const int ActivationCultureId = 7001;
    private const string Password = "CorrectHorseBattery12";
    private const string LegacyFarsi = "{\"Title\":\"عنوان موجود\"}";

    private readonly WebApplicationFactory<Program> _factory;

    public ContentActivationRegressionTests(TestWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ContentTranslation:ActivationCultureId", ActivationCultureId.ToString());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITranslationPort>();
                services.AddTransient<ITranslationPort, ThrowingTranslationPort>();
            });
        });
    }

    private sealed class ThrowingTranslationPort : ITranslationPort
    {
        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The translation provider must never be called from a request.");
    }

    private async Task<(HttpClient Client, int ApplicationId)> SignIn(bool superAdmin = true)
    {
        var email = $"content-activation-{Guid.NewGuid():N}@test.local";
        var user = superAdmin
            ? await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, Password)
            : await AccountFlowHelper.SeedAdminUserAsync(_factory, email, Password);
        var client = await AccountFlowHelper.LoginAsync(_factory, email, Password);
        return (client, await AccountFlowHelper.SelectApplicationAsync(_factory, client, user));
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private Task<int> SeedContent(int applicationId, bool isActive = false) => Db(async context =>
    {
        if (!await context.Cultures.IgnoreQueryFilters().AnyAsync(c => c.Id == ActivationCultureId))
            context.Cultures.Add(new Culture { Id = ActivationCultureId, ApplicationId = applicationId, Title = "Farsi", Key = "fa-IR", IsActive = true });

        var content = new Content
        {
            ApplicationId = applicationId, TypeId = 1000, Title = "English", PublishDt = DateTime.UtcNow,
            IsActive = isActive, FarsiContent = LegacyFarsi
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        return content.Id;
    });

    private Task SeedTranslation(int contentId, TranslationStatus status, bool currentSource = true) => Db(async context =>
    {
        var source = await context.Contents.AsNoTracking().Include(c => c.Metadata).Include(c => c.Sections).SingleAsync(c => c.Id == contentId);
        context.ContentTranslations.Add(new ContentTranslation
        {
            ContentId = contentId, CultureId = ActivationCultureId, TranslationStatus = status,
            SourceFingerprint = currentSource ? ContentSourceFingerprint.Compute(source) : new string('0', 64),
            LocalizedTextJson = "{}", Provider = "test", IsActive = true
        });
        return await context.SaveChangesAsync();
    });

    private Task<(bool IsActive, string FarsiContent)> ContentState(int contentId) =>
        Db(async context => await context.Contents.Where(c => c.Id == contentId).Select(c => new ValueTuple<bool, string>(c.IsActive, c.FarsiContent)).SingleAsync());

    private static async Task<string> TranslationState(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("translationState").GetString();

    private static Task<HttpResponseMessage> Activate(HttpClient client, int contentId, bool mode = true) =>
        client.PostAsync($"/BackOffice/Content/ChangeContentActiveMode/1000/{contentId}/{mode.ToString().ToLowerInvariant()}", null);

    [Fact]
    public async Task Activation_WithReadyTranslationOfCurrentSource_Activates()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);
        await SeedTranslation(contentId, TranslationStatus.Ready);

        var response = await Activate(client, contentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Done", await response.Content.ReadAsStringAsync());
        Assert.Equal((true, LegacyFarsi), await ContentState(contentId));
    }

    [Theory]
    [InlineData(null, true, "Missing")] // legacy FarsiContent alone is not readiness
    [InlineData(TranslationStatus.Ready, false, "Stale")]
    [InlineData(TranslationStatus.Stale, true, "Stale")]
    [InlineData(TranslationStatus.NeedsReview, true, "NeedsReview")]
    [InlineData(TranslationStatus.Failed, true, "Failed")]
    public async Task Activation_WithoutReadyTranslation_ConflictsAndStaysInactive(TranslationStatus? status, bool currentSource, string expectedState)
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);
        if (status != null)
            await SeedTranslation(contentId, status.Value, currentSource);

        var response = await Activate(client, contentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(expectedState, await TranslationState(response));
        Assert.Equal((false, LegacyFarsi), await ContentState(contentId));
    }

    [Fact]
    public async Task RequestTranslation_QueuesOneJob_AndActivationReportsQueued()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId);

        var first = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{contentId}/{ActivationCultureId}", null);
        var second = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{contentId}/{ActivationCultureId}", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Queued", firstBody.GetProperty("translationState").GetString());
        Assert.Equal(firstBody.GetProperty("jobId").GetInt32(), secondBody.GetProperty("jobId").GetInt32());
        Assert.Equal(1, await Db(c => c.ContentTranslationJobs.CountAsync(j => j.ContentId == contentId)));

        var activation = await Activate(client, contentId);
        Assert.Equal(HttpStatusCode.Conflict, activation.StatusCode);
        Assert.Equal("Queued", await TranslationState(activation));
        Assert.Equal((false, LegacyFarsi), await ContentState(contentId));
        Assert.False(await Db(c => c.ContentTranslations.AnyAsync(t => t.ContentId == contentId)));
    }

    [Fact]
    public async Task RequestTranslation_OtherApplicationsContentOrUnknownCulture_IsNotFound()
    {
        var (client, _) = await SignIn();
        var (_, otherApplicationId) = await SignIn();
        var foreignContentId = await SeedContent(otherApplicationId);

        var foreign = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{foreignContentId}/{ActivationCultureId}", null);
        var unknownCulture = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{foreignContentId}/999999", null);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownCulture.StatusCode);
        Assert.False(await Db(c => c.ContentTranslationJobs.AnyAsync(j => j.ContentId == foreignContentId)));
    }

    [Fact]
    public async Task RequestTranslation_WithoutChangeActivityAccess_IsForbidden()
    {
        var (client, applicationId) = await SignIn(superAdmin: false);
        var contentId = await SeedContent(applicationId);

        var response = await client.PostAsync($"/BackOffice/Content/RequestTranslation/{contentId}/{ActivationCultureId}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await Db(c => c.ContentTranslationJobs.AnyAsync(j => j.ContentId == contentId)));
    }

    [Fact]
    public async Task Deactivation_IsUnchanged_AndNeedsNoTranslation()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId, isActive: true);

        var response = await Activate(client, contentId, mode: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((false, LegacyFarsi), await ContentState(contentId));
    }
}
