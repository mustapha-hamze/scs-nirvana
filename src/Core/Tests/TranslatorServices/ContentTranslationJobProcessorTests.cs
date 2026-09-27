using System.Text.Json.Nodes;
using Application.CMSRepository;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.TranslatorServices;

// End-to-end over SQLite with the real job repository; the provider is always a fake.
public class ContentTranslationJobProcessorTests : IDisposable
{
    private const string Worker = "test-worker";
    private const string LegacyFarsi = "{\"Title\":\"legacy\"}";
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly ContentTranslationOptions _options = new() { LeaseMinutes = 10, MaxAttempts = 2 };

    public void Dispose() => _factory.Dispose();

    private sealed class FakePort(Func<TranslationRequest, CancellationToken, Task<TranslationResult>> handler) : ITranslationPort
    {
        public List<TranslationRequest> Requests { get; } = new();

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return handler(request, cancellationToken);
        }
    }

    // Structure-preserving fake translation: rewrites the word "English" in every string value.
    private static string FakeTranslate(string json)
    {
        var node = JsonNode.Parse(json);
        Walk(node);
        return node.ToJsonString();

        static void Walk(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                foreach (var (key, value) in obj.ToList())
                {
                    if (value is JsonValue v && v.TryGetValue<string>(out var text))
                        obj[key] = text.Replace("English", "فارسی");
                    else if (value != null)
                        Walk(value);
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array.Where(i => i != null))
                    Walk(item);
            }
        }
    }

    private static FakePort Translating() =>
        new((request, _) => Task.FromResult(TranslationResult.Ok(FakeTranslate(request.ContentJson), "fake", "fake-model")));

    private async Task<(int ContentId, int CultureId)> Seed()
    {
        await using var context = _factory.CreateContext();
        var culture = new Culture { ApplicationId = 1, Title = "Farsi", Key = "fa-IR" };
        var content = new Content
        {
            ApplicationId = 1, TypeId = 1000, Title = "English title", HeadLine = "English head", Abstract = null,
            Description = "<p>English <strong>desc</strong></p>", FarsiContent = LegacyFarsi, Categories = "1|2",
            Metadata = new ContentMetadata { Title = "English meta", Author = "English author", Keywords = "English keys" },
            Images = new List<ContentImage> { new() { ImageFileName = "secret-image.jpg" } },
            Sections = new List<ContentSection>
            {
                new()
                {
                    Priority = 1,
                    Elements = new List<SectionElement>
                    {
                        new() { ElementType = 1000, TinyText = "English tiny", EditorText = "<p>English<br></p>", FileNameText = "secret-file.pdf", GalleryImages = "secret-gallery", ElementTitle = "secret-element-title" }
                    }
                }
            }
        };
        context.AddRange(culture, content);
        await context.SaveChangesAsync();
        return (content.Id, culture.Id);
    }

    private async Task<string> Fingerprint(int contentId)
    {
        await using var context = _factory.CreateContext();
        return ContentSourceFingerprint.Compute(await new ContentTranslationJobRepository(context).FindSourceGraph(contentId));
    }

    private async Task<int> SeedJob(int contentId, int cultureId, string fingerprint = null,
        ContentTranslationJobState state = ContentTranslationJobState.Queued, DateTime? nextAttemptAt = null, DateTime? leaseExpiresAt = null)
    {
        fingerprint ??= await Fingerprint(contentId);
        await using var context = _factory.CreateContext();
        var job = new ContentTranslationJob
        {
            ContentId = contentId, CultureId = cultureId, SourceFingerprint = fingerprint, State = state,
            NextAttemptAt = nextAttemptAt ?? Now, LeaseExpiresAt = leaseExpiresAt, IsActive = true
        };
        context.ContentTranslationJobs.Add(job);
        await context.SaveChangesAsync();
        return job.Id;
    }

    private async Task<bool> RunOnce(ITranslationPort port, Func<ApplicationDbContext, IContentTranslationJobRepository> repository = null,
        CancellationToken stoppingToken = default)
    {
        await using var context = _factory.CreateContext();
        var sut = new ContentTranslationJobProcessor(repository?.Invoke(context) ?? new ContentTranslationJobRepository(context), port, _options, _clock);
        return await sut.RunOnce(Worker, stoppingToken);
    }

    private async Task<T> Read<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        await using var context = _factory.CreateContext();
        return await read(context);
    }

    private Task<ContentTranslationJob> Job(int id) => Read(c => c.ContentTranslationJobs.SingleAsync(j => j.Id == id));

    private Task<ContentTranslation> Row() => Read(c => c.ContentTranslations.IgnoreQueryFilters().SingleOrDefaultAsync());

    private Task<string> Legacy(int contentId) => Read(c => c.Contents.Where(x => x.Id == contentId).Select(x => x.FarsiContent).SingleAsync());

    private async Task AssertNoTranslationWrittenAndLegacyIntact(int contentId)
    {
        Assert.Null(await Row());
        Assert.Equal(LegacyFarsi, await Legacy(contentId));
    }

    [Fact]
    public async Task Success_SendsTextOnlyDocument_StoresReadyRow_AndLeavesLegacyUntouched()
    {
        var (contentId, cultureId) = await Seed();
        var fingerprint = await Fingerprint(contentId);
        var jobId = await SeedJob(contentId, cultureId);
        var port = Translating();

        Assert.True(await RunOnce(port));

        var request = Assert.Single(port.Requests);
        Assert.Equal("Farsi", request.TargetLanguage);
        Assert.Equal(TranslationSourceDocument.TranslatableFields, request.TranslatableFields);
        var document = JsonNode.Parse(request.ContentJson).AsObject();
        Assert.Equal(new[] { "Id", "Title", "HeadLine", "Abstract", "Description", "Metadata", "Sections" }, document.Select(p => p.Key));
        Assert.Equal(contentId, (int)document["Id"]);
        Assert.Equal(new[] { "Id", "Title", "Author", "Keywords", "Description" }, document["Metadata"].AsObject().Select(p => p.Key));
        var element = document["Sections"][0]["Elements"][0].AsObject();
        Assert.Equal(new[] { "Id", "TinyText", "EditorText" }, element.Select(p => p.Key));
        foreach (var excluded in new[] { "secret", "FarsiContent", "legacy", "Categories", "Images", "Priority", "IsActive", "UpdatedDT", "ElementTitle", "ApplicationId" })
            Assert.DoesNotContain(excluded, request.ContentJson, StringComparison.OrdinalIgnoreCase);

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Succeeded, 1, null, null, null, Now), (job.State, job.AttemptCount, job.ErrorCode, job.LeaseOwner, job.LeaseExpiresAt, job.CompletedAt));

        var row = await Row();
        Assert.Equal((TranslationStatus.Ready, fingerprint, "fake", "fake-model", Now, null),
            (row.TranslationStatus, row.SourceFingerprint, row.Provider, row.Model, row.TranslatedAt, row.Error));
        var text = LegacyFarsiContentParser.Deserialize(row.LocalizedTextJson);
        Assert.Equal(("فارسی title", "فارسی head", null, "<p>فارسی <strong>desc</strong></p>"), (text.Title, text.HeadLine, text.Abstract, text.Description));
        Assert.Equal(("فارسی author", "فارسی keys"), (text.Metadata.Author, text.Metadata.Keywords));
        Assert.Equal(new LocalizedElementText(element["Id"].GetValue<int>(), "فارسی tiny", "<p>فارسی<br></p>"), text.Sections.Single().Elements.Single());

        Assert.Equal(LegacyFarsi, await Legacy(contentId));
        Assert.False(await RunOnce(port));
        Assert.Single(port.Requests);
    }

    [Fact]
    public async Task Success_OverwritesStaleBackfillRow_InPlace()
    {
        var (contentId, cultureId) = await Seed();
        int rowId;
        await using (var context = _factory.CreateContext())
        {
            var stale = new ContentTranslation
            {
                ContentId = contentId, CultureId = cultureId, TranslationStatus = TranslationStatus.Stale, SourceFingerprint = "old",
                LocalizedTextJson = "{}", Provider = ContentTranslationBackfill.LegacyProvider, IsActive = true
            };
            context.ContentTranslations.Add(stale);
            await context.SaveChangesAsync();
            rowId = stale.Id;
        }
        await SeedJob(contentId, cultureId);

        await RunOnce(Translating());

        var row = await Row();
        Assert.Equal((rowId, TranslationStatus.Ready, "fake"), (row.Id, row.TranslationStatus, row.Provider));
    }

    [Fact]
    public async Task SourceChangedBeforeCall_SupersedesWithoutCallingProvider()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId, fingerprint: new string('0', 64));
        var port = Translating();

        Assert.True(await RunOnce(port));

        Assert.Empty(port.Requests);
        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Superseded, null), (job.State, job.ErrorCode));
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Fact]
    public async Task SourceChangedDuringCall_SupersedesWithoutStoring()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort(async (request, _) =>
        {
            await using var context = _factory.CreateContext();
            await context.Contents.Where(c => c.Id == contentId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Title, "Edited English"));
            return TranslationResult.Ok(FakeTranslate(request.ContentJson), "fake", "fake-model");
        });

        await RunOnce(port);

        Assert.Equal(ContentTranslationJobState.Superseded, (await Job(jobId)).State);
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Fact]
    public async Task DeletedContent_Supersedes()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        await using (var context = _factory.CreateContext())
            await context.Contents.Where(c => c.Id == contentId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true));
        var port = Translating();

        await RunOnce(port);

        Assert.Empty(port.Requests);
        Assert.Equal(ContentTranslationJobState.Superseded, (await Job(jobId)).State);
    }

    public static TheoryData<string, Func<string, string>> InvalidOutputs => new()
    {
        { "not json", _ => "{not json" },
        { "wrapped in markdown", json => "```json\n" + json + "\n```" },
        { "extra field", json => AddField(json, "Extra", "x") },
        { "missing field", json => RemoveField(json, "HeadLine") },
        { "id changed", json => json.Replace("\"Id\":", "\"Id\":9") },
        { "null became text", json => FakeTranslate(json).Replace("\"Abstract\":null", "\"Abstract\":\"x\"") },
        { "text became number", json => ReplaceField(json, "Title", 5) },
        { "html tag changed", json => FakeTranslate(json).Replace("strong", "em") },
        { "section dropped", json => ReplaceField(json, "Sections", new JsonArray()) },
    };

    private static string AddField(string json, string name, JsonNode value) { var o = JsonNode.Parse(json).AsObject(); o[name] = value; return o.ToJsonString(); }
    private static string RemoveField(string json, string name) { var o = JsonNode.Parse(json).AsObject(); o.Remove(name); return o.ToJsonString(); }
    private static string ReplaceField(string json, string name, JsonNode value) { var o = JsonNode.Parse(json).AsObject(); o[name] = value; return o.ToJsonString(); }

    [Theory]
    [MemberData(nameof(InvalidOutputs))]
    public async Task InvalidOutput_FailsJobOnly(string _, Func<string, string> corrupt)
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((request, _) => Task.FromResult(TranslationResult.Ok(corrupt(request.ContentJson), "fake", "fake-model")));

        await RunOnce(port);

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.InvalidOutput), (job.State, job.ErrorCode));
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Fact]
    public async Task NonRetryableProviderFailure_FailsWithSafeCode()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => Task.FromResult(TranslationResult.Failed("Protected field 'Id' was changed. <provider text>")));

        await RunOnce(port);

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.ProviderError), (job.State, job.ErrorCode));
        Assert.False(await RunOnce(port));
        Assert.Single(port.Requests);
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Fact]
    public async Task RateLimited_RetriesWithBackoff_ThenFailsWhenAttemptsExhausted()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => Task.FromResult(TranslationResult.Failed("rate limited", retryable: true)));

        await RunOnce(port);
        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Queued, 1, ContentTranslationErrorCodes.ProviderRateLimited, Now.AddSeconds(30), null),
            (job.State, job.AttemptCount, job.ErrorCode, job.NextAttemptAt, job.LeaseExpiresAt));

        Assert.False(await RunOnce(port)); // not due yet
        _clock.SetUtcNow(Now.AddSeconds(30));
        await RunOnce(port);

        job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, 2, ContentTranslationErrorCodes.ProviderRetriesExhausted), (job.State, job.AttemptCount, job.ErrorCode));
        Assert.Equal(2, port.Requests.Count);
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Fact]
    public async Task AmbiguousTimeout_FailsWithoutAutomaticResend()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => throw new TaskCanceledException("network timeout"));

        await RunOnce(port);

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.ProviderTimeout), (job.State, job.ErrorCode));
        _clock.SetUtcNow(Now.AddDays(1));
        Assert.False(await RunOnce(port));
        Assert.Single(port.Requests);
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Fact]
    public async Task ShutdownDuringCall_RecordsCancelledFailure()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        using var stopping = new CancellationTokenSource();
        var port = new FakePort((_, ct) =>
        {
            stopping.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(TranslationResult.Failed("unreachable"));
        });

        await RunOnce(port, stoppingToken: stopping.Token);

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.ProviderCancelled), (job.State, job.ErrorCode));
    }

    [Fact]
    public async Task ExpiredLease_FailsWithoutCallingProvider_ButLiveLeaseAndFutureJobsAreNotClaimed()
    {
        var (contentId, cultureId) = await Seed();
        var expired = await SeedJob(contentId, cultureId, fingerprint: new string('1', 64), state: ContentTranslationJobState.Processing, leaseExpiresAt: Now.AddSeconds(-1));
        var live = await SeedJob(contentId, cultureId, fingerprint: new string('2', 64), state: ContentTranslationJobState.Processing, leaseExpiresAt: Now.AddMinutes(5));
        var future = await SeedJob(contentId, cultureId, nextAttemptAt: Now.AddMinutes(1));
        var port = Translating();

        Assert.True(await RunOnce(port));
        Assert.False(await RunOnce(port));

        Assert.Empty(port.Requests);
        var job = await Job(expired);
        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.LeaseExpired), (job.State, job.ErrorCode));
        Assert.Equal(ContentTranslationJobState.Processing, (await Job(live)).State);
        Assert.Equal(ContentTranslationJobState.Queued, (await Job(future)).State);
    }

    [Fact]
    public async Task ConcurrentClaim_LosesOnVersion_AndNeverCallsProvider()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = Translating();

        Assert.True(await RunOnce(port, context => new RacingRepository(context)));

        Assert.Empty(port.Requests);
        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Queued, 0, 1), (job.State, job.AttemptCount, job.Version));
    }

    [Fact]
    public async Task Claim_SetsLeaseAndVersion()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        ContentTranslationJob during = null;
        var port = new FakePort(async (request, _) =>
        {
            during = await Job(jobId);
            return TranslationResult.Ok(FakeTranslate(request.ContentJson), "fake", "fake-model");
        });

        await RunOnce(port);

        Assert.Equal((ContentTranslationJobState.Processing, 1, Worker, Now.AddMinutes(10), 1),
            (during.State, during.AttemptCount, during.LeaseOwner, during.LeaseExpiresAt, during.Version));
        Assert.Equal(2, (await Job(jobId)).Version);
    }

    [Fact]
    public async Task MatchingReadyRowWrittenConcurrently_IsPreserved()
    {
        var (contentId, cultureId) = await Seed();
        var fingerprint = await Fingerprint(contentId);
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort(async (request, _) =>
        {
            await using var context = _factory.CreateContext();
            context.ContentTranslations.Add(new ContentTranslation
            {
                ContentId = contentId, CultureId = cultureId, TranslationStatus = TranslationStatus.Ready, SourceFingerprint = fingerprint,
                LocalizedTextJson = "{\"title\":\"manual\"}", Provider = "manual", IsActive = true
            });
            await context.SaveChangesAsync();
            return TranslationResult.Ok(FakeTranslate(request.ContentJson), "fake", "fake-model");
        });

        await RunOnce(port);

        var row = await Row();
        Assert.Equal(("manual", "{\"title\":\"manual\"}"), (row.Provider, row.LocalizedTextJson));
        Assert.Equal(ContentTranslationJobState.Succeeded, (await Job(jobId)).State);
    }

    [Fact]
    public async Task DeletedCulture_FailsWithoutCallingProvider()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        await using (var context = _factory.CreateContext())
            await context.Cultures.Where(c => c.Id == cultureId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true));
        var port = Translating();

        await RunOnce(port);

        Assert.Empty(port.Requests);
        Assert.Equal(ContentTranslationErrorCodes.CultureUnavailable, (await Job(jobId)).ErrorCode);
    }

    // Another worker claims the job between this worker's read and its claim save.
    private sealed class RacingRepository(ApplicationDbContext context) : IContentTranslationJobRepository
    {
        private readonly ContentTranslationJobRepository _inner = new(context);

        public async Task<ContentTranslationJob> FindNextClaimable(DateTime utcNow, CancellationToken ct = default)
        {
            var job = await _inner.FindNextClaimable(utcNow, ct);
            await context.Database.ExecuteSqlRawAsync("UPDATE CMS_ContentTranslationJobs SET Version = Version + 1", ct);
            return job;
        }

        public Task<bool> ContentBelongsToApplication(int contentId, int applicationId, CancellationToken ct = default) => _inner.ContentBelongsToApplication(contentId, applicationId, ct);
        public Task<Culture> FindCulture(int cultureId, CancellationToken ct = default) => _inner.FindCulture(cultureId, ct);
        public Task<Content> FindSourceGraph(int contentId, CancellationToken ct = default) => _inner.FindSourceGraph(contentId, ct);
        public Task<ContentTranslation> FindTranslation(int contentId, int cultureId, CancellationToken ct = default) => _inner.FindTranslation(contentId, cultureId, ct);
        public void AddTranslation(ContentTranslation translation) => _inner.AddTranslation(translation);
        public Task<ContentTranslationJob> FindJob(int contentId, int cultureId, string fingerprint, CancellationToken ct = default) => _inner.FindJob(contentId, cultureId, fingerprint, ct);
        public void AddJob(ContentTranslationJob job) => _inner.AddJob(job);
        public Task<bool> TrySaveChanges(CancellationToken ct = default) => _inner.TrySaveChanges(ct);
    }

    [Fact]
    public void JobModel_MatchesDbaContract()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused").Options;
        using var context = new ApplicationDbContext(options, TimeProvider.System);
        var entity = context.Model.FindEntityType(typeof(ContentTranslationJob));

        Assert.Equal("CMS_ContentTranslationJobs", entity.GetTableName());
        Assert.NotNull(entity.GetDeclaredQueryFilters().SingleOrDefault());
        foreach (var (name, type, nullable) in new[]
                 {
                     (nameof(ContentTranslationJob.ContentId), "int", false), (nameof(ContentTranslationJob.CultureId), "int", false),
                     (nameof(ContentTranslationJob.SourceFingerprint), "varchar(64)", false), (nameof(ContentTranslationJob.State), "tinyint", false),
                     (nameof(ContentTranslationJob.AttemptCount), "int", false), (nameof(ContentTranslationJob.NextAttemptAt), "datetime2", false),
                     (nameof(ContentTranslationJob.LeaseOwner), "varchar(100)", true), (nameof(ContentTranslationJob.LeaseExpiresAt), "datetime2", true),
                     (nameof(ContentTranslationJob.ErrorCode), "varchar(64)", true), (nameof(ContentTranslationJob.CompletedAt), "datetime2", true),
                     (nameof(ContentTranslationJob.Version), "int", false)
                 })
        {
            var property = entity.FindProperty(name);
            Assert.Equal((name, type, nullable), (name, property.GetColumnType(), property.IsNullable));
        }

        Assert.True(entity.FindProperty(nameof(ContentTranslationJob.Version)).IsConcurrencyToken);
        var unique = entity.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(new[] { "ContentId", "CultureId", "SourceFingerprint" }, unique.Properties.Select(p => p.Name));
        Assert.Contains(entity.GetIndexes(), i => !i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "State", "NextAttemptAt" }));
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
