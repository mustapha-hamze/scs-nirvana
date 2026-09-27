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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Web.Tests;

// Phase 2 of the legacy Farsi translation queue: the page and read-only candidate endpoint are
// SuperAdmin-only (a Content access key is not enough), read only the selected application's
// content for the configured culture whatever the request says, return only the safe candidate
// page contract, and never queue, translate, or write.
public sealed class LegacyFarsiTranslationQueueHttpTests : IClassFixture<TestWebApplicationFactory>
{
    private const int ActivationCultureId = 7101;
    private const int OtherCultureId = 7102;
    private const int TypeId = 1000;
    private const string Password = "CorrectHorseBattery12";
    private const string LegacyFarsi = "{\"Title\":\"عنوان قدیمی\"}";
    private const string PageUrl = "/BackOffice/LegacyFarsiTranslationQueue/Index";
    private const string CandidatesUrl = "/BackOffice/LegacyFarsiTranslationQueue/Candidates";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly SaveCounter _saves = new();

    public LegacyFarsiTranslationQueueHttpTests(TestWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ContentTranslation:ActivationCultureId", ActivationCultureId.ToString());
            builder.UseSetting("ContentTranslation:LegacyBulkCandidateTypeIds:0", TypeId.ToString());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITranslationPort>();
                services.AddTransient<ITranslationPort, ThrowingTranslationPort>();
                services.ConfigureDbContext<ApplicationDbContext>(options => options.AddInterceptors(_saves));
            });
        });
    }

    private sealed class ThrowingTranslationPort : ITranslationPort
    {
        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The translation provider must never be called from a request.");
    }

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Count);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref Count);
            return base.SavingChanges(eventData, result);
        }
    }

    private async Task<(HttpClient Client, int ApplicationId)> SignIn(bool superAdmin = true, string? keys = null)
    {
        var email = $"legacy-queue-{Guid.NewGuid():N}@test.local";
        var user = superAdmin
            ? await AccountFlowHelper.SeedSuperAdminUserAsync(_factory, email, Password)
            : await AccountFlowHelper.SeedAdminUserAsync(_factory, email, Password);
        var client = await AccountFlowHelper.LoginAsync(_factory, email, Password);
        var applicationId = await AccountFlowHelper.SelectApplicationAsync(_factory, client, user);
        if (keys != null)
            await AccountFlowHelper.GrantAccessAsync(_factory, user, applicationId, keys);
        return (client, applicationId);
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private Task<int> NewApplication() => Db(async context =>
    {
        var application = new Domains.Entities.General.Application { Title = $"Other {Guid.NewGuid():N}", IsActive = true };
        context.Applications.Add(application);
        await context.SaveChangesAsync();
        return application.Id;
    });

    private Task<int> SeedContent(int applicationId, string title) => Db(async context =>
    {
        foreach (var cultureId in new[] { ActivationCultureId, OtherCultureId })
            if (!await context.Cultures.IgnoreQueryFilters().AnyAsync(c => c.Id == cultureId))
                context.Cultures.Add(new Culture { Id = cultureId, ApplicationId = applicationId, Title = "Farsi", Key = $"fa-{cultureId}", IsActive = true });

        var content = new Content
        {
            ApplicationId = applicationId, TypeId = TypeId, Title = title, PublishDt = DateTime.UtcNow, FarsiContent = LegacyFarsi
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        return content.Id;
    });

    private Task SeedTranslation(int contentId, int cultureId) => Db(async context =>
    {
        context.ContentTranslations.Add(new ContentTranslation
        {
            ContentId = contentId, CultureId = cultureId, TranslationStatus = TranslationStatus.Ready,
            SourceFingerprint = new string('a', 64), LocalizedTextJson = "{\"Title\":\"raw-translation-json\"}", Provider = "secret-provider", IsActive = true
        });
        return await context.SaveChangesAsync();
    });

    private Task SeedFailedJob(int contentId) => Db(async context =>
    {
        context.ContentTranslationJobs.Add(new ContentTranslationJob
        {
            ContentId = contentId, CultureId = ActivationCultureId, SourceFingerprint = new string('b', 64),
            State = ContentTranslationJobState.Failed, ErrorCode = "secret-job-error", NextAttemptAt = DateTime.UtcNow
        });
        return await context.SaveChangesAsync();
    });

    private static async Task<int[]> ItemIds(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("contentId").GetInt32()).ToArray();
    }

    [Theory]
    [InlineData(PageUrl)]
    [InlineData(CandidatesUrl)]
    public async Task Anonymous_RedirectsToLogin(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PageUrl, null)]
    [InlineData(CandidatesUrl, null)]
    [InlineData(PageUrl, "content-keys")]
    [InlineData(CandidatesUrl, "content-keys")]
    public async Task NonSuperAdmin_EvenWithChangeActivity_IsDenied(string url, string? keys)
    {
        var grant = keys == null ? null : string.Join(',', Web.Authorization.AccessKeys.Content.Module, Web.Authorization.AccessKeys.Content.ChangeActivity);
        var (client, applicationId) = await SignIn(superAdmin: false, keys: grant);
        await SeedContent(applicationId, "Hidden from members");

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuperAdmin_GetsPage()
    {
        var (client, _) = await SignIn();

        var response = await client.GetAsync(PageUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Legacy Farsi Translation Queue", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SuperAdmin_GetsOnlySelectedApplicationsCandidates_InSafeShape()
    {
        var (client, applicationId) = await SignIn();
        var candidateId = await SeedContent(applicationId, "Mine");
        await SeedFailedJob(candidateId);
        var foreignId = await SeedContent(await NewApplication(), "Foreign");

        var response = await client.GetAsync(CandidatesUrl);

        Assert.Equal(new[] { candidateId }, await ItemIds(response));
        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(new[] { "cultureAvailable", "items", "totalCount", "page", "pageSize" }, body.EnumerateObject().Select(p => p.Name));
        Assert.True(body.GetProperty("cultureAvailable").GetBoolean());
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(LegacyFarsiTranslationCandidates.DefaultPageSize, body.GetProperty("pageSize").GetInt32());
        var item = body.GetProperty("items")[0];
        Assert.Equal(new[] { "contentId", "title", "typeId", "isActive", "updatedAt" }, item.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Mine", item.GetProperty("title").GetString());
        Assert.DoesNotContain("قدیمی", raw);
        Assert.DoesNotContain("secret-job-error", raw);
        Assert.DoesNotContain("Foreign", raw);
        Assert.DoesNotContain(foreignId.ToString(), body.GetProperty("items").ToString());
    }

    [Fact]
    public async Task ApplicationAndCultureInTheRequest_AreIgnored()
    {
        var (client, applicationId) = await SignIn();
        var candidateId = await SeedContent(applicationId, "Mine");
        var translatedId = await SeedContent(applicationId, "Already translated");
        await SeedTranslation(translatedId, ActivationCultureId);
        var otherApplicationId = await NewApplication();
        var foreignId = await SeedContent(otherApplicationId, "Foreign");

        var tampered = $"applicationId={otherApplicationId}&ApplicationId={otherApplicationId}&cultureId={OtherCultureId}&activationCultureId={OtherCultureId}";
        Assert.Equal(new[] { candidateId }, await ItemIds(await client.GetAsync($"{CandidatesUrl}?{tampered}")));
        Assert.Empty(await ItemIds(await client.GetAsync($"{CandidatesUrl}?{tampered}&contentId={foreignId}")));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{CandidatesUrl}/{otherApplicationId}")).StatusCode);
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("99")]
    [InlineData("FarsiContent")]
    public async Task UnknownSort_IsRejected(string sort)
    {
        var (client, _) = await SignIn();

        var response = await client.GetAsync($"{CandidatesUrl}?sort={sort}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TypedSortAndPaging_AreApplied()
    {
        var (client, applicationId) = await SignIn();
        var a = await SeedContent(applicationId, "A");
        var b = await SeedContent(applicationId, "B");
        var c = await SeedContent(applicationId, "C");

        var response = await client.GetAsync($"{CandidatesUrl}?sort=Title&descending=true&page=2&pageSize=2");

        Assert.Equal(new[] { a }, await ItemIds(response));
        Assert.Equal(new[] { c, b }, await ItemIds(await client.GetAsync($"{CandidatesUrl}?sort=Title&descending=true&pageSize=2")));
    }

    [Fact]
    public async Task PageAndCandidateReads_QueueNothing_CallNoProvider_AndWriteNothing()
    {
        var (client, applicationId) = await SignIn();
        var candidateId = await SeedContent(applicationId, "Mine");
        var before = await Db(context => context.Contents.AsNoTracking().SingleAsync(x => x.Id == candidateId));
        Assert.True(Interlocked.Exchange(ref _saves.Count, 0) > 0); // the interceptor sees this host's writes

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(PageUrl)).StatusCode);
        Assert.Equal(new[] { candidateId }, await ItemIds(await client.GetAsync(CandidatesUrl)));

        Assert.Equal(0, _saves.Count);
        Assert.False(await Db(context => context.ContentTranslationJobs.AnyAsync(j => j.ContentId == candidateId)));
        Assert.False(await Db(context => context.ContentTranslations.AnyAsync(t => t.ContentId == candidateId)));
        var after = await Db(context => context.Contents.AsNoTracking().SingleAsync(x => x.Id == candidateId));
        Assert.Equal((before.FarsiContent, before.UpdatedDT, before.IsActive), (after.FarsiContent, after.UpdatedDT, after.IsActive));
    }
}
