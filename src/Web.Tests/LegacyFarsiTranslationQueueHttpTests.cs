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
// page contract, and never queue, translate, or write. Phase 3: the bulk queue POST is SuperAdmin-only
// and antiforgery-protected, accepts only content IDs, re-checks each one server-side, and returns
// only a safe per-item outcome. Phase 5: the progress GET is SuperAdmin-only, reads only the selected
// application's jobs for the configured culture, and returns only a safe per-job state. The worker is
// off, so the provider can only be reached inline.
public sealed class LegacyFarsiTranslationQueueHttpTests : IClassFixture<TestWebApplicationFactory>
{
    private const int ActivationCultureId = 7101;
    private const int OtherCultureId = 7102;
    private const int TypeId = 1000;
    private const string Password = "CorrectHorseBattery12";
    private const string LegacyFarsi = "{\"Title\":\"عنوان قدیمی\"}";
    private const string PageUrl = "/BackOffice/LegacyFarsiTranslationQueue/Index";
    private const string CandidatesUrl = "/BackOffice/LegacyFarsiTranslationQueue/Candidates";

    private const string QueueUrl = "/BackOffice/LegacyFarsiTranslationQueue/Queue";
    private const string ProgressUrl = "/BackOffice/LegacyFarsiTranslationQueue/Progress";

    private WebApplicationFactory<Program> _factory;
    private readonly SaveCounter _saves = new();

    public LegacyFarsiTranslationQueueHttpTests(TestWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ContentTranslation:ActivationCultureId", ActivationCultureId.ToString());
            builder.UseSetting("ContentTranslation:WorkerEnabled", "false");
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
    [InlineData(ProgressUrl + "?jobIds=1")]
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
    [InlineData(ProgressUrl + "?jobIds=1", null)]
    [InlineData(ProgressUrl + "?jobIds=1", "content-keys")]
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
        Assert.DoesNotContain(foreignId, body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("contentId").GetInt32()));
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

    private static Task<HttpResponseMessage> PostQueue(HttpClient client, string json) =>
        client.PostAsync(QueueUrl, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> PostQueue(HttpClient client, params int[] ids) =>
        PostQueue(client, JsonSerializer.Serialize(new { contentIds = ids }));

    private static async Task<(int ContentId, string Outcome, int? JobId)[]> Outcomes(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "items" }, body.EnumerateObject().Select(p => p.Name));
        return body.GetProperty("items").EnumerateArray().Select(i =>
        {
            Assert.Equal(new[] { "contentId", "outcome", "jobId" }, i.EnumerateObject().Select(p => p.Name));
            var jobId = i.GetProperty("jobId");
            return (i.GetProperty("contentId").GetInt32(), i.GetProperty("outcome").GetString()!, jobId.ValueKind == JsonValueKind.Null ? (int?)null : jobId.GetInt32());
        }).ToArray();
    }

    private Task<ContentTranslationJob[]> Jobs(params int[] contentIds) =>
        Db(context => context.ContentTranslationJobs.AsNoTracking().Where(j => contentIds.Contains(j.ContentId)).ToArrayAsync());

    private Task<string?> Legacy(int contentId) =>
        Db(context => context.Contents.AsNoTracking().Where(c => c.Id == contentId).Select(c => (string?)c.FarsiContent).SingleAsync());

    [Theory]
    [InlineData(null)]
    [InlineData("content-keys")]
    public async Task Queue_NonSuperAdmin_EvenWithChangeActivity_IsDenied_AndQueuesNothing(string? keys)
    {
        var grant = keys == null ? null : string.Join(',', Web.Authorization.AccessKeys.Content.Module, Web.Authorization.AccessKeys.Content.ChangeActivity);
        var (client, applicationId) = await SignIn(superAdmin: false, keys: grant);
        var contentId = await SeedContent(applicationId, "Member");

        var response = await PostQueue(client, contentId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await Jobs(contentId));
    }

    [Fact]
    public async Task Queue_WithoutAntiforgeryToken_IsRejected_AndQueuesNothing()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId, "Mine");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        Assert.Equal(HttpStatusCode.BadRequest, (await PostQueue(client, contentId)).StatusCode);
        Assert.Empty(await Jobs(contentId));
    }

    [Fact]
    public async Task Queue_SuperAdmin_QueuesDistinctIdsInOrder_Idempotently_InSafeShape()
    {
        var (client, applicationId) = await SignIn();
        var a = await SeedContent(applicationId, "A");
        var b = await SeedContent(applicationId, "B");
        var legacyBefore = await Legacy(a);
        Assert.NotNull(legacyBefore);
        var legacyBytes = System.Text.Encoding.UTF8.GetBytes(legacyBefore);

        var first = await PostQueue(client, b, a, b);
        var raw = await first.Content.ReadAsStringAsync();
        var queued = await Outcomes(first);
        var second = await Outcomes(await PostQueue(client, a, b));

        var jobs = await Jobs(a, b);
        Assert.Equal(2, jobs.Length);
        Assert.All(jobs, j => Assert.Equal((ActivationCultureId, ContentTranslationJobState.Queued), (j.CultureId, j.State)));
        int JobOf(int id) => jobs.Single(j => j.ContentId == id).Id;
        Assert.Equal(new[] { (b, "Queued", (int?)JobOf(b)), (a, "Queued", JobOf(a)) }, queued);
        Assert.Equal(new[] { (a, "AlreadyQueued", (int?)JobOf(a)), (b, "AlreadyQueued", JobOf(b)) }, second);
        Assert.DoesNotContain("قدیمی", raw);
        Assert.DoesNotContain(jobs[0].SourceFingerprint, raw);
        var legacyAfter = await Legacy(a);
        Assert.NotNull(legacyAfter);
        Assert.Equal(legacyBytes, System.Text.Encoding.UTF8.GetBytes(legacyAfter));
    }

    [Theory]
    [InlineData("{\"contentIds\":[]}")]
    [InlineData("{\"contentIds\":[0]}")]
    [InlineData("{\"contentIds\":[1,-5]}")]
    [InlineData("{\"contentIds\":[\"x\"]}")]
    [InlineData("{\"contentIds\":null}")]
    [InlineData("{}")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("over-limit")]
    [InlineData("over-limit-duplicates")]
    public async Task Queue_InvalidRequest_Is400_AndQueuesNothing(string json)
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId, "Mine");
        var limit = new ContentTranslationOptions().BulkRequestMaxItems;
        if (json.StartsWith("over-limit"))
            json = JsonSerializer.Serialize(new
            {
                contentIds = json == "over-limit" ? Enumerable.Range(contentId, limit + 1) : Enumerable.Repeat(contentId, limit + 1)
            });

        var response = await PostQueue(client, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"1 to {limit}", await response.Content.ReadAsStringAsync());
        Assert.Empty(await Jobs(contentId));
    }

    [Fact]
    public async Task Queue_ApplicationCultureAndJobDataInTheRequest_AreIgnored()
    {
        var (client, applicationId) = await SignIn();
        var mine = await SeedContent(applicationId, "Mine");
        var otherApplicationId = await NewApplication();
        var foreign = await SeedContent(otherApplicationId, "Foreign");
        var tampered = JsonSerializer.Serialize(new
        {
            contentIds = new[] { foreign, mine },
            applicationId = otherApplicationId, cultureId = OtherCultureId, activationCultureId = OtherCultureId,
            sourceFingerprint = new string('f', 64), state = "Succeeded", provider = "evil", eligible = true
        });

        var items = await Outcomes(await PostQueue(client, tampered));

        var job = Assert.Single(await Jobs(mine, foreign));
        Assert.Equal(new[] { (foreign, "NotFound", (int?)null), (mine, "Queued", (int?)job.Id) }, items);
        Assert.Equal((mine, ActivationCultureId, ContentTranslationJobState.Queued), (job.ContentId, job.CultureId, job.State));
        Assert.NotEqual(new string('f', 64), job.SourceFingerprint);
    }

    [Fact]
    public async Task Queue_RevalidatesCandidatesListedEarlier()
    {
        var (client, applicationId) = await SignIn();
        var translated = await SeedContent(applicationId, "Translated");
        var deleted = await SeedContent(applicationId, "Deleted");
        var retyped = await SeedContent(applicationId, "Retyped");
        var cleared = await SeedContent(applicationId, "Cleared");
        var moved = await SeedContent(applicationId, "Moved");
        var all = new[] { translated, deleted, retyped, cleared, moved };
        Assert.Equal(all, (await ItemIds(await client.GetAsync($"{CandidatesUrl}?pageSize=100"))).Where(all.Contains));

        await SeedTranslation(translated, ActivationCultureId);
        var otherApplicationId = await NewApplication();
        await Db(async context =>
        {
            foreach (var content in await context.Contents.Where(c => all.Contains(c.Id)).ToListAsync())
            {
                if (content.Id == deleted) content.IsDeleted = true;
                if (content.Id == retyped) content.TypeId = 999; // not in the configured allow-list
                if (content.Id == cleared) content.FarsiContent = " ";
                if (content.Id == moved) content.ApplicationId = otherApplicationId;
            }
            return await context.SaveChangesAsync();
        });

        var items = await Outcomes(await PostQueue(client, all));

        Assert.Equal(new[]
        {
            (translated, "Skipped", (int?)null), (deleted, "NotFound", null), (retyped, "Skipped", null), (cleared, "Skipped", null), (moved, "NotFound", null)
        }, items);
        Assert.Empty(await Jobs(all));
        var raw = await (await PostQueue(client, translated)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("raw-translation-json", raw);
        Assert.DoesNotContain("secret-provider", raw);
    }

    [Fact]
    public async Task Queue_CultureUnavailable_QueuesAndWritesNothing()
    {
        _factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("ContentTranslation:ActivationCultureId", "7199"));
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId, "Mine");
        Interlocked.Exchange(ref _saves.Count, 0);

        var items = await Outcomes(await PostQueue(client, contentId, contentId));

        Assert.Equal(new[] { (contentId, "CultureUnavailable", (int?)null) }, items);
        Assert.Equal(0, _saves.Count);
        Assert.Empty(await Jobs(contentId));
    }

    private Task<int> SeedJob(int contentId, ContentTranslationJobState state, int cultureId = ActivationCultureId, int attempts = 0,
        string? errorCode = null, bool isDeleted = false) => Db(async context =>
    {
        var job = new ContentTranslationJob
        {
            ContentId = contentId, CultureId = cultureId, SourceFingerprint = Guid.NewGuid().ToString("N").PadRight(64, 'c'), State = state,
            AttemptCount = attempts, ErrorCode = errorCode, NextAttemptAt = DateTime.UtcNow, LeaseOwner = "secret-lease-owner", IsDeleted = isDeleted
        };
        context.ContentTranslationJobs.Add(job);
        await context.SaveChangesAsync();
        return job.Id;
    });

    private static string ProgressQuery(params int[] jobIds) => ProgressUrl + "?" + string.Join('&', jobIds.Select(id => $"jobIds={id}"));

    private static async Task<(int JobId, int? ContentId, string State, int? AttemptCount, string? ErrorCode)[]> States(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "items" }, body.EnumerateObject().Select(p => p.Name));
        static int? Int(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();
        return body.GetProperty("items").EnumerateArray().Select(i =>
        {
            Assert.Equal(new[] { "jobId", "contentId", "state", "attemptCount", "errorCode" }, i.EnumerateObject().Select(p => p.Name));
            return (i.GetProperty("jobId").GetInt32(), Int(i.GetProperty("contentId")), i.GetProperty("state").GetString()!,
                Int(i.GetProperty("attemptCount")), i.GetProperty("errorCode").GetString());
        }).ToArray();
    }

    [Fact]
    public async Task Progress_SuperAdmin_GetsOnlySelectedApplicationsJobs_InSafeShape_WhateverTheRequestSays()
    {
        var (client, applicationId) = await SignIn();
        var mine = await SeedContent(applicationId, "Mine");
        var queued = await SeedJob(mine, ContentTranslationJobState.Queued, attempts: 1, errorCode: "lease_expired");
        var failed = await SeedJob(mine, ContentTranslationJobState.Failed, attempts: 3, errorCode: ContentTranslationErrorCodes.ProviderError);
        var succeeded = await SeedJob(mine, ContentTranslationJobState.Succeeded, attempts: 1);
        var otherCulture = await SeedJob(mine, ContentTranslationJobState.Succeeded, cultureId: OtherCultureId);
        var deleted = await SeedJob(mine, ContentTranslationJobState.Processing, isDeleted: true);
        var otherApplicationId = await NewApplication();
        var foreign = await SeedJob(await SeedContent(otherApplicationId, "Foreign"), ContentTranslationJobState.Failed, attempts: 2, errorCode: "provider_timeout");
        var tampered = $"&applicationId={otherApplicationId}&ApplicationId={otherApplicationId}&cultureId={OtherCultureId}&activationCultureId={OtherCultureId}";

        var response = await client.GetAsync(ProgressQuery(foreign, queued, failed, queued, succeeded, otherCulture, deleted) + tampered);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(new[]
        {
            (foreign, (int?)null, "NotFound", (int?)null, (string?)null), (queued, mine, "Queued", 1, null),
            (failed, mine, "Failed", 3, ContentTranslationErrorCodes.ProviderError), (succeeded, mine, "Succeeded", 1, null),
            (otherCulture, null, "NotFound", null, null), (deleted, null, "NotFound", null, null)
        }, await States(response));
        foreach (var secret in new[] { "قدیمی", "Foreign", "provider_timeout", "secret-lease-owner", "Mine", "fingerprint", "cccc" })
            Assert.DoesNotContain(secret, raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?jobIds=")]
    [InlineData("?jobIds=0")]
    [InlineData("?jobIds=1&jobIds=-5")]
    [InlineData("?jobIds=x")]
    [InlineData("?jobIds=1.5")]
    [InlineData("?jobIds=99999999999")]
    [InlineData("over-limit")]
    [InlineData("over-limit-duplicates")]
    public async Task Progress_InvalidRequest_Is400(string query)
    {
        var (client, applicationId) = await SignIn();
        var job = await SeedJob(await SeedContent(applicationId, "Mine"), ContentTranslationJobState.Queued);
        var limit = new ContentTranslationOptions().BulkRequestMaxItems;
        var url = query switch
        {
            "over-limit" => ProgressQuery(Enumerable.Range(job, limit + 1).ToArray()),
            "over-limit-duplicates" => ProgressQuery(Enumerable.Repeat(job, limit + 1).ToArray()),
            _ => ProgressUrl + query
        };

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("items", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Progress_QueuesNothing_CallsNoProvider_AndWritesNothing()
    {
        var (client, applicationId) = await SignIn();
        var contentId = await SeedContent(applicationId, "Mine");
        var job = await SeedJob(contentId, ContentTranslationJobState.Queued);
        var before = (await Jobs(contentId)).Single();
        Interlocked.Exchange(ref _saves.Count, 0);

        Assert.Equal(new[] { (job, (int?)contentId, "Queued", (int?)0, (string?)null) }, await States(await client.GetAsync(ProgressQuery(job, job))));

        Assert.Equal(0, _saves.Count);
        var after = Assert.Single(await Jobs(contentId));
        Assert.Equal((before.State, before.AttemptCount, before.Version, before.NextAttemptAt), (after.State, after.AttemptCount, after.Version, after.NextAttemptAt));
        Assert.False(await Db(context => context.ContentTranslations.AnyAsync(t => t.ContentId == contentId)));
    }
}
