using System.Text.Json.Nodes;
using Application.CMSRepository;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    private readonly ListLogger _logger = new();

    public void Dispose() => _factory.Dispose();

    // Records rendered log lines so tests can prove only approved fields are logged.
    private sealed class ListLogger : ILogger<ContentTranslationJobProcessor>
    {
        public List<(LogLevel Level, string Message, Exception Exception)> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class FakePort(Func<TranslationRequest, CancellationToken, Task<TranslationResult>> handler) : ITranslationPort
    {
        public List<TranslationRequest> Requests { get; } = new();

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return handler(request, cancellationToken);
        }
    }

    // Fake translation of every slot value: rewrites the word "English".
    private static TranslationResult FakeTranslate(TranslationRequest request) =>
        TranslationResult.OkTexts(request.Texts.Select(t => t.Replace("English", "فارسی")).ToList(), "fake", "fake-model");

    private static FakePort Translating() => new((request, _) => Task.FromResult(FakeTranslate(request)));

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
        var sut = new ContentTranslationJobProcessor(repository?.Invoke(context) ?? new ContentTranslationJobRepository(context), port, _options, _clock, _logger);
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
    public async Task Success_SendsOnlyOrderedTextSlots_StoresReadyRow_AndLeavesLegacyUntouched()
    {
        var (contentId, cultureId) = await Seed();
        var fingerprint = await Fingerprint(contentId);
        var jobId = await SeedJob(contentId, cultureId);
        var port = Translating();

        Assert.True(await RunOnce(port));

        var request = Assert.Single(port.Requests);
        Assert.Equal(("Farsi", null, null), (request.TargetLanguage, request.ContentJson, request.TranslatableFields));
        Assert.Equal(new[] { "English title", "English head", "English", "desc", "English meta", "English author", "English keys", "English tiny", "English" },
            request.Texts);
        var elementId = await Read(c => c.SectionElements.Select(e => e.Id).SingleAsync());

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Succeeded, 1, null, null, null, Now), (job.State, job.AttemptCount, job.ErrorCode, job.LeaseOwner, job.LeaseExpiresAt, job.CompletedAt));

        var row = await Row();
        Assert.Equal((TranslationStatus.Ready, fingerprint, "fake", "fake-model", Now, null),
            (row.TranslationStatus, row.SourceFingerprint, row.Provider, row.Model, row.TranslatedAt, row.Error));
        var text = LegacyFarsiContentParser.Deserialize(row.LocalizedTextJson);
        Assert.Equal(("فارسی title", "فارسی head", null, "<p>فارسی <strong>desc</strong></p>"), (text.Title, text.HeadLine, text.Abstract, text.Description));
        Assert.Equal(("فارسی author", "فارسی keys"), (text.Metadata.Author, text.Metadata.Keywords));
        Assert.Equal(new LocalizedElementText(elementId, "فارسی tiny", "<p>فارسی<br></p>"), text.Sections.Single().Elements.Single());

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
            return FakeTranslate(request);
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

    // Slot values the processor can't rebuild into a valid document (the real port rejects most of
    // these earlier as invalid_response; see the OpenAI-port cases below).
    public static TheoryData<string, Func<IReadOnlyList<string>, IReadOnlyList<string>>> InvalidOutputs => new()
    {
        { "no texts", _ => null },
        { "too few", texts => texts.Skip(1).ToList() },
        { "too many", texts => texts.Append("x").ToList() },
        { "null value", texts => texts.Select((t, i) => i == 0 ? null : t).ToList() },
        { "blank value", texts => texts.Select((t, i) => i == 0 ? " " : t).ToList() },
        { "markup in plain text", texts => texts.Select((t, i) => i == 0 ? "<b>x</b>" : t).ToList() },
    };

    [Theory]
    [MemberData(nameof(InvalidOutputs))]
    public async Task InvalidOutput_FailsJobOnly(string _, Func<IReadOnlyList<string>, IReadOnlyList<string>> corrupt)
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((request, _) => Task.FromResult(TranslationResult.OkTexts(corrupt(request.Texts), "fake", "fake-model")));

        await RunOnce(port);

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.InvalidOutput), (job.State, job.ErrorCode));
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    // A full-document result (the old port shape) carries no slot values: never stored.
    [Fact]
    public async Task DocumentResult_IsNotAcceptedAsSlots()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);

        await RunOnce(new FakePort((_, _) => Task.FromResult(TranslationResult.Ok("{\"Title\":\"x\"}", "fake", "fake-model"))));

        Assert.Equal(ContentTranslationErrorCodes.InvalidOutput, (await Job(jobId)).ErrorCode);
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Fact]
    public async Task MalformedSourceMarkup_FailsWithoutCallingProvider()
    {
        var (contentId, cultureId) = await Seed();
        await using (var context = _factory.CreateContext())
            await context.SectionElements.ExecuteUpdateAsync(s => s.SetProperty(e => e.EditorText, "<p>English</b></p>"));
        var jobId = await SeedJob(contentId, cultureId);
        var port = Translating();

        await RunOnce(port);

        Assert.Empty(port.Requests);
        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.InvalidOutput), (job.State, job.ErrorCode));
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Fact]
    public async Task NoTranslatableText_SucceedsWithoutCallingProvider_KeepingNullsAndEmpties()
    {
        int contentId, cultureId;
        await using (var context = _factory.CreateContext())
        {
            var culture = new Culture { ApplicationId = 1, Title = "Farsi", Key = "fa-IR" };
            var content = new Content
            {
                ApplicationId = 1, TypeId = 1000, Title = "", HeadLine = null, Description = "  ", FarsiContent = LegacyFarsi,
                Sections = [new() { Elements = [new() { ElementType = 1000, TinyText = "", EditorText = "<p><br></p>" }] }]
            };
            context.AddRange(culture, content);
            await context.SaveChangesAsync();
            (contentId, cultureId) = (content.Id, culture.Id);
        }
        var jobId = await SeedJob(contentId, cultureId);
        var port = Translating();

        await RunOnce(port);

        Assert.Empty(port.Requests);
        Assert.Equal(ContentTranslationJobState.Succeeded, (await Job(jobId)).State);
        var text = LegacyFarsiContentParser.Deserialize((await Row()).LocalizedTextJson);
        Assert.Equal(("", null, null, "  "), (text.Title, text.HeadLine, text.Abstract, text.Description));
        Assert.Equal(new LocalizedElementText(text.Sections.Single().Elements.Single().Id, "", "<p><br></p>"), text.Sections.Single().Elements.Single());
        Assert.Equal(LegacyFarsi, await Legacy(contentId));
    }

    // ---- Through the real OpenAI port over a fake HTTP transport (no network, no key). ----

    private sealed class FakeHandler(Func<string, string> content) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            var completion = System.Text.Json.JsonSerializer.Serialize(new
            {
                id = "chatcmpl-test", @object = "chat.completion", created = 0, model = "test-model-2026",
                choices = new[] { new { index = 0, finish_reason = "stop", message = new { role = "assistant", content = content(body) } } }
            });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(completion, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private static Infrastructure.TranslatorServices.OpenAiTranslationPort OpenAiPort(FakeHandler handler) =>
        new(Microsoft.Extensions.Options.Options.Create(new Infrastructure.TranslatorServices.OpenAiTranslationOptions { ApiKey = "test-secret-key", Model = "test-model" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.TranslatorServices.OpenAiTranslationPort>.Instance,
            new System.ClientModel.Primitives.HttpClientPipelineTransport(new HttpClient(handler)));

    // The texts array of the request's user message.
    private static List<string> SentTexts(string body)
    {
        var envelope = JsonNode.Parse((string)JsonNode.Parse(body)!["messages"]![1]!["content"]!)!.AsObject();
        Assert.Equal(new[] { "targetLanguage", "texts" }, envelope.Select(p => p.Key));
        return envelope["texts"]!.AsArray().Select(t => (string)t!).ToList();
    }

    private static string Respond(IEnumerable<string> translations) =>
        new JsonObject { [TranslationTextSlots.ResponseProperty] = new JsonArray(translations.Select(t => (JsonNode)t).ToArray()) }.ToJsonString();

    private const string RichHtml =
        "<p class=\"lead\" data-x='English'>English <a href=\"https://example.test/English\" target=\"_blank\" rel=\"noopener\">read   more</a>&nbsp;now</p>"
        + "<!-- English editor note --><p><br></p><img src=\"English.jpg\" alt=\"English alt\">"
        + "<script>var English = \"<p>English</p>\";</script><style>.English { color: red; }</style>\n<ul><li> English item </li></ul>";

    // Sections/elements are inserted out of order; slots follow Priority, then Id.
    private async Task<(int ContentId, int CultureId)> SeedComplex()
    {
        await using var context = _factory.CreateContext();
        var culture = new Culture { ApplicationId = 1, Title = "Farsi", Key = "fa-IR" };
        SectionElement Element(int id, string tiny, string editor) => new()
        {
            Id = id, ElementType = 1000, TinyText = tiny, EditorText = editor, FileNameText = "secret-file.pdf", GalleryImages = "secret-gallery",
            ElementTitle = "secret-element-title", Size = 3
        };
        var content = new Content
        {
            ApplicationId = 1, TypeId = 1000, Title = "English title", HeadLine = null, Abstract = "", Description = "English & <b>bold</b>",
            FarsiContent = LegacyFarsi, Categories = "secret-cat", Tags = "secret-tag", Cultures = "secret-culture",
            Metadata = new ContentMetadata { Title = "English meta", Author = null, Keywords = "English keys", Description = "" },
            Images = [new() { ImageFileName = "secret-image.jpg" }],
            Sections =
            [
                new() { Id = 50, Priority = 2, Elements = [Element(501, "Last tiny", null)] },
                new() { Id = 70, Priority = 1, Elements = [Element(702, "Second of 70", "<p>Plain <em>emphasis</em></p>"), Element(701, "First of 70", "")] },
                new() { Id = 60, Priority = 1, Elements = [Element(601, null, RichHtml)] }
            ]
        };
        context.AddRange(culture, content);
        await context.SaveChangesAsync();
        return (content.Id, culture.Id);
    }

    private static readonly string[] ComplexTexts =
    [
        "English title", "English & ", "bold", // Description: markup, so text nodes (decoded, trimmed)
        "English meta", "English keys",
        "English", "read   more", " now", "English item", // section 60 / element 601 (visible text only)
        "First of 70", "Second of 70", "Plain", "emphasis", // section 70: elements 701, 702
        "Last tiny" // section 50
    ];

    [Fact]
    public async Task TextSlots_SendOnlyVisibleText_AndRebuildTheCanonicalDocumentAroundTheTranslations()
    {
        var (contentId, cultureId) = await SeedComplex();
        var fingerprint = await Fingerprint(contentId);
        var jobId = await SeedJob(contentId, cultureId);
        // Each value becomes "ف" + value; one returns markup-like text, which must stay text.
        var handler = new FakeHandler(body => Respond(SentTexts(body).Select(t => t == "bold" ? "<i onclick=\"x\">a</i> & b" : "ف" + t)));

        await RunOnce(OpenAiPort(handler));

        var body = Assert.Single(handler.Bodies);
        Assert.Equal(ComplexTexts.Select(t => t.Trim(' ')), SentTexts(body));
        foreach (var excluded in new[] { "secret", "legacy", "\"Id\"", "Sections", "Metadata", "href", "example.test", "noopener", "class", "lead",
                     "editor note", "var English", "color: red", "English.jpg", "English alt", "<p", "\\u003Cp" })
            Assert.DoesNotContain(excluded, body, StringComparison.OrdinalIgnoreCase);

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Succeeded, null), (job.State, job.ErrorCode));
        var row = await Row();
        Assert.Equal((TranslationStatus.Ready, fingerprint, OpenAiTranslationPortName, "test-model-2026"), (row.TranslationStatus, row.SourceFingerprint, row.Provider, row.Model));

        var metadataId = await Read(c => c.ContentMetadatas.Select(m => m.Id).SingleAsync());
        var expected = new LocalizedContentText("فEnglish title", null, "", "فEnglish &amp; <b>&lt;i onclick=\"x\"&gt;a&lt;/i&gt; &amp; b</b>",
            new LocalizedMetadataText(metadataId, "فEnglish meta", null, "فEnglish keys", ""),
            [
                new(60, [new(601, null,
                    "<p class=\"lead\" data-x='English'>فEnglish <a href=\"https://example.test/English\" target=\"_blank\" rel=\"noopener\">فread   more</a>ف\u00A0now</p>"
                    + "<!-- English editor note --><p><br></p><img src=\"English.jpg\" alt=\"English alt\">"
                    + "<script>var English = \"<p>English</p>\";</script><style>.English { color: red; }</style>\n<ul><li> فEnglish item </li></ul>")]),
                new(70, [new(701, "فFirst of 70", ""), new(702, "فSecond of 70", "<p>فPlain <em>فemphasis</em></p>")]),
                new(50, [new(501, "فLast tiny", null)])
            ]);
        Assert.Equal(LegacyFarsiContentParser.Serialize(expected), row.LocalizedTextJson);
        Assert.Equal(LegacyFarsi, await Legacy(contentId));
    }

    private const string OpenAiTranslationPortName = Infrastructure.TranslatorServices.OpenAiTranslationPort.ProviderName;

    // Seed() has 9 slots.
    public static TheoryData<string, string, string> BadSlotResponses => new()
    {
        { "invalid json", "{not json", ContentTranslationErrorCodes.InvalidJson },
        { "missing array", "{}", ContentTranslationErrorCodes.InvalidResponse },
        { "renamed array", Respond(Enumerable.Repeat("ت", 9)).Replace(TranslationTextSlots.ResponseProperty, "texts"), ContentTranslationErrorCodes.InvalidResponse },
        { "extra property", Respond(Enumerable.Repeat("ت", 9)).TrimEnd('}') + ",\"note\":\"x\"}", ContentTranslationErrorCodes.InvalidResponse },
        { "not an array", "{\"" + TranslationTextSlots.ResponseProperty + "\":\"ت\"}", ContentTranslationErrorCodes.InvalidResponse },
        { "too few", Respond(Enumerable.Repeat("ت", 8)), ContentTranslationErrorCodes.InvalidResponse },
        { "too many", Respond(Enumerable.Repeat("ت", 10)), ContentTranslationErrorCodes.InvalidResponse },
        { "null value", Respond(Enumerable.Repeat("ت", 8)).Replace("[", "[null,"), ContentTranslationErrorCodes.InvalidResponse },
        { "number value", Respond(Enumerable.Repeat("ت", 8)).Replace("[", "[5,"), ContentTranslationErrorCodes.InvalidResponse },
        { "blank value", Respond(Enumerable.Repeat("ت", 8).Append(" ")), ContentTranslationErrorCodes.InvalidResponse },
        // Regression: the old full-document reply (here with a changed ID) is simply not a slot reply.
        { "full document", "{\"Id\":9,\"Title\":\"ت\",\"Sections\":[]}", ContentTranslationErrorCodes.InvalidResponse },
    };

    [Theory]
    [MemberData(nameof(BadSlotResponses))]
    public async Task BadSlotResponse_FailsWithAFixedCode_WithoutWritingOrTouchingLegacy(string _, string response, string code)
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);

        await RunOnce(OpenAiPort(new FakeHandler(_ => response)));

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, code), (job.State, job.ErrorCode));
        Assert.NotNull(ContentTranslationErrorCodes.FailureReasonFor(job.State, job.ErrorCode));
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("ت") || e.Message.Contains("English"));
    }

    // Every terminal port classification is stored as its own safe code, never resent, and never
    // writes a translation or touches FarsiContent.
    [Theory]
    [InlineData(ContentTranslationErrorCodes.ProviderRejected)]
    [InlineData(ContentTranslationErrorCodes.ProviderNetwork)]
    [InlineData(ContentTranslationErrorCodes.ProviderError)]
    [InlineData(ContentTranslationErrorCodes.EmptyResponse)]
    [InlineData(ContentTranslationErrorCodes.InvalidJson)]
    [InlineData(ContentTranslationErrorCodes.InvalidStructure)]
    [InlineData(ContentTranslationErrorCodes.InvalidResponse)]
    public async Task TerminalPortFailure_FailsWithItsOwnCode_WithoutResend(string code)
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => Task.FromResult(TranslationResult.Failed(code, "<provider text> English title", httpStatus: 401, exceptionType: "SomeException")));

        await RunOnce(port);

        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, 1, code, Now), (job.State, job.AttemptCount, job.ErrorCode, job.CompletedAt));
        _clock.SetUtcNow(Now.AddDays(1));
        Assert.False(await RunOnce(port));
        Assert.Single(port.Requests);
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);

        var (level, message, exception) = Assert.Single(_logger.Entries);
        Assert.Equal((LogLevel.Warning, null), (level, exception));
        Assert.Equal($"Content translation job {jobId} (content {contentId}, attempt 1) failed with {code} (HTTP 401, SomeException)", message);
    }

    // Regression: a validator failure used to be collapsed into provider_error.
    [Fact]
    public async Task ValidatorFailure_IsStoredAndShownAsInvalidStructure_NotProviderError()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => Task.FromResult(TranslationResult.Failed(ContentTranslationErrorCodes.InvalidStructure, "Protected field 'Id' was changed.")));

        await RunOnce(port);

        var job = await Job(jobId);
        Assert.Equal(ContentTranslationErrorCodes.InvalidStructure, job.ErrorCode);
        Assert.Equal("The translation response changed required content structure. Try again.",
            ContentTranslationErrorCodes.FailureReasonFor(job.State, job.ErrorCode));
        Assert.DoesNotContain("Protected", string.Join(" ", _logger.Entries.Select(e => e.Message)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("secret provider text {\"raw\":1}")]
    [InlineData(ContentTranslationErrorCodes.LeaseExpired)] // a real code, but not one a port may report
    public async Task UnknownPortCode_IsStoredAsProviderError(string code)
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => Task.FromResult(TranslationResult.Failed(code)));

        await RunOnce(port);

        Assert.Equal((ContentTranslationJobState.Failed, ContentTranslationErrorCodes.ProviderError), ((await Job(jobId)).State, (await Job(jobId)).ErrorCode));
        Assert.DoesNotContain("secret", string.Join(" ", _logger.Entries.Select(e => e.Message)));
    }

    [Theory]
    [InlineData(ContentTranslationErrorCodes.ProviderRateLimited)]
    [InlineData(ContentTranslationErrorCodes.ProviderTransient)]
    public async Task RetryableRejection_RetriesWithBackoff_ThenFailsWhenAttemptsExhausted(string code)
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => Task.FromResult(TranslationResult.Failed(code)));

        await RunOnce(port);
        var job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Queued, 1, code, Now.AddSeconds(30), null, null),
            (job.State, job.AttemptCount, job.ErrorCode, job.NextAttemptAt, job.LeaseExpiresAt, job.CompletedAt));
        Assert.Null(ContentTranslationErrorCodes.FailureReasonFor(job.State, job.ErrorCode));

        Assert.False(await RunOnce(port)); // not due yet
        _clock.SetUtcNow(Now.AddSeconds(30));
        await RunOnce(port);

        job = await Job(jobId);
        Assert.Equal((ContentTranslationJobState.Failed, 2, ContentTranslationErrorCodes.ProviderRetriesExhausted), (job.State, job.AttemptCount, job.ErrorCode));
        Assert.Equal(2, port.Requests.Count);
        await AssertNoTranslationWrittenAndLegacyIntact(contentId);
    }

    [Theory]
    [InlineData(90, 90)]
    [InlineData(7200, 1800)] // capped
    public async Task RetryAfter_OverridesBackoff_UpToTheCap(int retryAfterSeconds, int expectedSeconds)
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => Task.FromResult(TranslationResult.Failed(ContentTranslationErrorCodes.ProviderRateLimited,
            retryAfter: TimeSpan.FromSeconds(retryAfterSeconds), httpStatus: 429)));

        await RunOnce(port);

        Assert.Equal(Now.AddSeconds(expectedSeconds), (await Job(jobId)).NextAttemptAt);
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
        Assert.Equal($"Content translation job {jobId} (content {contentId}, attempt 1) failed with provider_timeout (HTTP (null), TaskCanceledException)",
            Assert.Single(_logger.Entries).Message);
    }

    [Fact]
    public async Task UnexpectedException_IsLoggedWithJobIdAndFixedKind_WithoutItsMessage_AndPropagates()
    {
        var (contentId, cultureId) = await Seed();
        var jobId = await SeedJob(contentId, cultureId);
        var port = new FakePort((_, _) => throw new InvalidOperationException("secret English title"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunOnce(port));

        var (level, message, exception) = Assert.Single(_logger.Entries);
        Assert.Equal((LogLevel.Error, null), (level, exception));
        Assert.Equal($"Content translation job {jobId} (content {contentId}, attempt 1) failed with processing_error InvalidOperationException", message);
        Assert.Equal(ContentTranslationJobState.Processing, (await Job(jobId)).State); // lease expiry fails it later
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
            return FakeTranslate(request);
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
            return FakeTranslate(request);
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
