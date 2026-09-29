using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.UseCases.TranslatorServices;
using Infrastructure.TranslatorServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.Tests.TranslatorServices;

// The real SDK over a fake HTTP handler: no network, no key. Proves what goes on the wire and how
// every provider outcome is classified.
public class OpenAiTranslationPortTests
{
    private const string Document = "{\"Id\":7,\"Title\":\"English title\",\"Sections\":[{\"Id\":3,\"TinyText\":\"<p>English</p>\"}]}";
    private const string Translated = "{\"Id\":7,\"Title\":\"فارسی\",\"Sections\":[{\"Id\":3,\"TinyText\":\"<p>فارسی</p>\"}]}";

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return await respond(request, cancellationToken);
        }
    }

    private sealed class ListLogger : ILogger<OpenAiTranslationPort>
    {
        public List<string> Messages { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Assert.Null(exception); // an exception object would carry its message into the log
            Messages.Add(formatter(state, exception));
        }
    }

    private readonly ListLogger _logger = new();

    private OpenAiTranslationPort Port(FakeHandler handler) =>
        new(Options.Create(new OpenAiTranslationOptions { ApiKey = "test-secret-key", Model = "test-model" }), _logger,
            new HttpClientPipelineTransport(new HttpClient(handler)));

    private static Task<HttpResponseMessage> Respond(HttpStatusCode status, string body = "{\"error\":{\"message\":\"secret provider text\"}}",
        string retryAfter = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (retryAfter != null)
            response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        return Task.FromResult(response);
    }

    private static Task<HttpResponseMessage> Completion(string content) => Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new
    {
        id = "chatcmpl-test", @object = "chat.completion", created = 0, model = "test-model-2026",
        choices = new[] { new { index = 0, finish_reason = "stop", message = new { role = "assistant", content } } }
    }));

    private async Task<(TranslationResult Result, FakeHandler Handler)> Translate(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var handler = new FakeHandler(respond);
        var result = await Port(handler).TranslateAsync(new TranslationRequest(Document, ["Title", "TinyText"], "Farsi"));
        return (result, handler);
    }

    private void AssertLogsAreSafe()
    {
        foreach (var message in _logger.Messages)
            foreach (var secret in new[] { "secret", "English", "فارسی", "test-secret-key", "{", "\"v", "v0", "v1", "missing_property" })
                Assert.DoesNotContain(secret, message);
    }

    [Fact]
    public async Task Success_RequiresJsonObjectOutput_AndReturnsTheValidatedTranslation()
    {
        var (result, handler) = await Translate((_, _) => Completion(Translated));

        Assert.True(result.Success);
        Assert.Equal((Translated, OpenAiTranslationPort.ProviderName, "test-model-2026"), (result.TranslatedJson, result.Provider, result.Model));
        var body = JsonNode.Parse(Assert.Single(handler.Bodies))!;
        Assert.Equal("json_object", (string)body["response_format"]!["type"]!);
        Assert.Equal("test-model", (string)body["model"]!);
        Assert.Contains("JSON", (string)body["messages"]![0]!["content"]!); // json_object mode requires the word in the prompt
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ContentTranslationErrorCodes.ProviderRateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ContentTranslationErrorCodes.ProviderTransient)]
    [InlineData(HttpStatusCode.BadRequest, ContentTranslationErrorCodes.ProviderRejected)]
    [InlineData(HttpStatusCode.Unauthorized, ContentTranslationErrorCodes.ProviderRejected)]
    [InlineData(HttpStatusCode.Forbidden, ContentTranslationErrorCodes.ProviderRejected)]
    [InlineData(HttpStatusCode.NotFound, ContentTranslationErrorCodes.ProviderRejected)]
    [InlineData(HttpStatusCode.RequestTimeout, ContentTranslationErrorCodes.ProviderError)]
    [InlineData(HttpStatusCode.InternalServerError, ContentTranslationErrorCodes.ProviderError)]
    [InlineData(HttpStatusCode.BadGateway, ContentTranslationErrorCodes.ProviderError)]
    [InlineData(HttpStatusCode.GatewayTimeout, ContentTranslationErrorCodes.ProviderError)]
    public async Task HttpFailure_IsClassifiedByStatus_AndNeverResentBySdk(HttpStatusCode status, string code)
    {
        var (result, handler) = await Translate((_, _) => Respond(status));

        Assert.False(result.Success);
        Assert.Equal((code, (int)status), (result.FailureCode, result.HttpStatus));
        Assert.DoesNotContain("secret", result.Error);
        Assert.Single(handler.Bodies);
        Assert.Contains($"{code} (HTTP {(int)status}, ClientResultException)", Assert.Single(_logger.Messages));
        AssertLogsAreSafe();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task RetryableRejection_CarriesRetryAfter(HttpStatusCode status)
    {
        var (result, _) = await Translate((_, _) => Respond(status, retryAfter: "42"));

        Assert.Equal(TimeSpan.FromSeconds(42), result.RetryAfter);
    }

    [Fact]
    public async Task NonRetryableFailure_IgnoresRetryAfter()
    {
        var (result, _) = await Translate((_, _) => Respond(HttpStatusCode.InternalServerError, retryAfter: "42"));

        Assert.Null(result.RetryAfter);
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError, ContentTranslationErrorCodes.ProviderNetwork)]
    [InlineData(HttpRequestError.ConnectionError, ContentTranslationErrorCodes.ProviderNetwork)]
    [InlineData(HttpRequestError.SecureConnectionError, ContentTranslationErrorCodes.ProviderNetwork)]
    [InlineData(HttpRequestError.ResponseEnded, ContentTranslationErrorCodes.ProviderError)] // may have been processed
    [InlineData(HttpRequestError.Unknown, ContentTranslationErrorCodes.ProviderError)]
    public async Task TransportFailure_IsNetworkOnlyWhenConnectingFailed_AndNeverResent(HttpRequestError error, string code)
    {
        var (result, handler) = await Translate((_, _) => throw new HttpRequestException(error, "secret host detail"));

        Assert.Equal((code, (int?)null), (result.FailureCode, result.HttpStatus));
        Assert.Single(handler.Bodies);
        AssertLogsAreSafe();
    }

    [Fact]
    public async Task Cancellation_Propagates_ForTheCallerToRecordWithoutResend()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHandler(async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return null!;
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Port(handler).TranslateAsync(new TranslationRequest(Document), cts.Token));
        Assert.Single(handler.Bodies);
    }

    public static TheoryData<string, string> BadContent => new()
    {
        { "", ContentTranslationErrorCodes.EmptyResponse },
        { "   ", ContentTranslationErrorCodes.EmptyResponse },
        { "{not json", ContentTranslationErrorCodes.InvalidJson },
        { "```json\n" + Translated + "\n```", ContentTranslationErrorCodes.InvalidJson },
        { Translated.Replace("\"Id\":3", "\"Id\":4"), ContentTranslationErrorCodes.InvalidStructure },
        { Translated.Replace("<p>فارسی</p>", "فارسی"), ContentTranslationErrorCodes.InvalidStructure },
        { "{\"Id\":7}", ContentTranslationErrorCodes.InvalidStructure }
    };

    [Theory]
    [MemberData(nameof(BadContent))]
    public async Task UnusableContent_IsClassified_AndLoggedWithoutContent(string content, string code)
    {
        var (result, _) = await Translate((_, _) => Completion(content));

        Assert.Equal((false, code), (result.Success, result.FailureCode));
        AssertLogsAreSafe();
    }

    [Fact]
    public async Task NoChoices_IsAnEmptyResponse()
    {
        var (result, _) = await Translate((_, _) => Respond(HttpStatusCode.OK,
            "{\"id\":\"x\",\"object\":\"chat.completion\",\"created\":0,\"model\":\"m\",\"choices\":[]}"));

        Assert.Equal(ContentTranslationErrorCodes.EmptyResponse, result.FailureCode);
    }

    // ---- Text-slot requests (background jobs). ----

    private static readonly string[] Texts = ["English title", "English <b>not markup</b> & more", "English third"];

    private static string Slots(params string[] values) =>
        new JsonObject(values.Select((v, i) => KeyValuePair.Create(TranslationTextSlots.SlotKey(i), (JsonNode)v))).ToJsonString();

    private async Task<(TranslationResult Result, FakeHandler Handler)> TranslateTexts(string content, IReadOnlyList<string> texts = null)
    {
        var handler = new FakeHandler((_, _) => Completion(content));
        var result = await Port(handler).TranslateAsync(TranslationRequest.ForTexts(texts ?? Texts, "Farsi"));
        return (result, handler);
    }

    [Fact]
    public async Task Texts_SendOnlyTheEnvelope_WithAnExactStrictSchema_AndReturnTheOrderedValues()
    {
        var (result, handler) = await TranslateTexts("{\"v2\":\"فارسی ۳\",\"v0\":\"فارسی ۱\",\"v1\":\"فارسی ۲\"}");

        Assert.True(result.Success);
        Assert.Equal(new[] { "فارسی ۱", "فارسی ۲", "فارسی ۳" }, result.TranslatedTexts); // bound by key, not response order
        Assert.Equal((OpenAiTranslationPort.ProviderName, "test-model-2026", null), (result.Provider, result.Model, result.TranslatedJson));

        var body = JsonNode.Parse(Assert.Single(handler.Bodies))!;
        var format = body["response_format"]!;
        Assert.Equal(("json_schema", true), ((string)format["type"]!, (bool)format["json_schema"]!["strict"]!));
        // Exactly one required, non-null string per slot, nothing else: the provider can't omit or add a value.
        Assert.Equal("""
            {"type":"object","properties":{"v0":{"type":"string"},"v1":{"type":"string"},"v2":{"type":"string"}},"required":["v0","v1","v2"],"additionalProperties":false}
            """, format["json_schema"]!["schema"]!.ToJsonString());

        var messages = body["messages"]!.AsArray();
        Assert.Equal(new[] { "system", "user" }, messages.Select(m => (string)m!["role"]!));
        var prompt = (string)messages[0]!["content"]!;
        Assert.DoesNotContain("English", prompt); // instructions carry no source text
        foreach (var rule in new[] { "\"v0\"", "every property", "non-empty", "No other properties", "no markdown", "no commentary" })
            Assert.Contains(rule, prompt);
        var envelope = JsonNode.Parse((string)messages[1]!["content"]!)!.AsObject();
        Assert.Equal(new[] { "targetLanguage", "texts" }, envelope.Select(p => p.Key));
        Assert.Equal("Farsi", (string)envelope["targetLanguage"]!);
        Assert.Equal(Texts, envelope["texts"]!.AsArray().Select(t => (string)t!));
    }

    // Every malformed shape of a 3-slot reply: one provider call, invalid_response with a fixed reason
    // and counts only (invalid_json/empty_response keep their own codes).
    public static TheoryData<string, string, string, int?> BadTexts => new()
    {
        { "", ContentTranslationErrorCodes.EmptyResponse, null, null },
        { "{not json", ContentTranslationErrorCodes.InvalidJson, null, null },
        { "```json\n" + Slots("a", "b", "c") + "\n```", ContentTranslationErrorCodes.InvalidJson, null, null },
        { "[\"a\",\"b\",\"c\"]", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.WrongValueType, null },
        { "{}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.MissingProperty, 0 },
        { Slots("a", "b"), ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.MissingProperty, 2 },
        { "{\"v0\":\"a\",\"v2\":\"c\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.MissingProperty, 2 },
        { Slots("a", "b", "c", "d"), ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.UnexpectedProperty, 4 },
        { Slots("a", "b", "c").TrimEnd('}') + ",\"note\":\"x\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.UnexpectedProperty, 4 },
        { "{\"v0\":\"a\",\"v01\":\"b\",\"v2\":\"c\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.UnexpectedProperty, 3 },
        { "{\"v0\":\"a\",\"V1\":\"b\",\"v2\":\"c\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.UnexpectedProperty, 3 },
        { "{\"v0\":\"a\",\"v1\":\"b\",\"v1\":\"x\",\"v2\":\"c\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.DuplicateProperty, 4 },
        { "{\"v0\":\"a\",\"v1\":null,\"v2\":\"c\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.WrongValueType, 3 },
        { "{\"v0\":\"a\",\"v1\":2,\"v2\":\"c\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.WrongValueType, 3 },
        { "{\"v0\":\"a\",\"v1\":{\"t\":\"b\"},\"v2\":\"c\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.WrongValueType, 3 },
        { "{\"v0\":\"a\",\"v1\":[\"b\"],\"v2\":\"c\"}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.WrongValueType, 3 },
        { Slots("a", "  ", "c"), ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.BlankValue, 3 },
        // The pre-hardening array reply, even with the right count, is no longer accepted.
        { "{\"translations\":[\"a\",\"b\",\"c\"]}", ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.UnexpectedProperty, 1 },
        { Translated, ContentTranslationErrorCodes.InvalidResponse, TranslationResponseShape.UnexpectedProperty, 3 }
    };

    [Theory]
    [MemberData(nameof(BadTexts))]
    public async Task Texts_UnusableContent_IsClassified_WithASafeShape_AndLoggedWithoutContent(string content, string code, string reason, int? actual)
    {
        var (result, handler) = await TranslateTexts(content);

        Assert.Equal((false, code, null), (result.Success, result.FailureCode, result.TranslatedTexts));
        Assert.Equal(code, result.Error);
        Assert.Equal(reason == null ? null : new TranslationResponseShape(reason, 3, actual), result.ResponseShape);
        Assert.Single(handler.Bodies);
        Assert.Equal($"Translation failed with {code}", Assert.Single(_logger.Messages).Split(" (HTTP")[0]);
        AssertLogsAreSafe();
    }

    // Above MaxSlotsPerRequest, consecutive chunks each get their own exact schema; values come back in order.
    [Fact]
    public async Task Texts_OverTheSchemaLimit_GoInOrderedChunks_EachWithItsOwnExactSchema()
    {
        var texts = Enumerable.Range(0, 2 * OpenAiTranslationPort.MaxSlotsPerRequest + 5).Select(i => $"English {i}").ToList();
        var handler = new FakeHandler(async (request, ct) =>
        {
            var sent = JsonNode.Parse((string)JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!["messages"]![1]!["content"]!)!["texts"]!.AsArray();
            return await Completion(Slots(sent.Select(t => ((string)t!).Replace("English", "فارسی")).ToArray()));
        });

        var result = await Port(handler).TranslateAsync(TranslationRequest.ForTexts(texts, "Farsi"));

        Assert.True(result.Success);
        Assert.Equal(texts.Select(t => t.Replace("English", "فارسی")), result.TranslatedTexts);
        Assert.Equal(new[] { 100, 100, 5 }, handler.Bodies.Select(b => JsonNode.Parse(b)!["response_format"]!["json_schema"]!["schema"]!["required"]!.AsArray().Count));
        Assert.All(handler.Bodies, b => Assert.Equal(false, (bool)JsonNode.Parse(b)!["response_format"]!["json_schema"]!["schema"]!["additionalProperties"]!));
    }

    [Fact]
    public async Task Texts_AFailedChunk_FailsTheWholeRequest_WithoutFurtherCalls()
    {
        var texts = Enumerable.Range(0, 3 * OpenAiTranslationPort.MaxSlotsPerRequest).Select(i => $"English {i}").ToList();
        var calls = 0;
        var handler = new FakeHandler((_, _) => Completion(++calls == 2
            ? Slots(Enumerable.Repeat("ف", OpenAiTranslationPort.MaxSlotsPerRequest - 1).ToArray())
            : Slots(Enumerable.Repeat("ف", OpenAiTranslationPort.MaxSlotsPerRequest).ToArray())));

        var result = await Port(handler).TranslateAsync(TranslationRequest.ForTexts(texts, "Farsi"));

        Assert.Equal((false, ContentTranslationErrorCodes.InvalidResponse, null), (result.Success, result.FailureCode, result.TranslatedTexts));
        Assert.Equal(new TranslationResponseShape(TranslationResponseShape.MissingProperty, 100, 99), result.ResponseShape);
        Assert.Equal(2, handler.Bodies.Count);
    }

    [Fact]
    public async Task Texts_HttpFailure_IsClassifiedLikeDocuments()
    {
        var handler = new FakeHandler((_, _) => Respond(HttpStatusCode.TooManyRequests, retryAfter: "7"));

        var result = await Port(handler).TranslateAsync(TranslationRequest.ForTexts(Texts, "Farsi"));

        Assert.Equal((ContentTranslationErrorCodes.ProviderRateLimited, 429, TimeSpan.FromSeconds(7)), (result.FailureCode, result.HttpStatus, result.RetryAfter));
        AssertLogsAreSafe();
    }
}
