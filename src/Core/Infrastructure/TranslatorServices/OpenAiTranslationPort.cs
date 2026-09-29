using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Infrastructure.TranslatorServices;

// The only place the OpenAI SDK is touched. Never logs or returns the API key, request/response
// content, headers or exception messages - only fixed failure codes, HTTP status and exception type.
public class OpenAiTranslationPort : ITranslationPort
{
    public const string ProviderName = "openai";

    private readonly ChatClient _client;
    private readonly string _model;
    private readonly ILogger<OpenAiTranslationPort> _logger;

    // transport: test seam only (a fake HTTP handler); production uses the SDK default.
    public OpenAiTranslationPort(IOptions<OpenAiTranslationOptions> options, ILogger<OpenAiTranslationPort> logger,
        PipelineTransport transport = null)
    {
        var settings = options.Value;
        _model = settings.Model;
        // RetryPolicy: the SDK by default silently resends on 408/429/5xx and timeouts. Chat
        // completions have no idempotency key, so a resend after an ambiguous failure can bill
        // (and translate) twice; ContentTranslationErrorCodes.IsRetryable decides instead.
        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromMinutes(settings.NetworkTimeoutMinutes),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };
        if (transport != null)
            clientOptions.Transport = transport;
        _client = new ChatClient(settings.Model, new ApiKeyCredential(settings.ApiKey), clientOptions);
        _logger = logger;
    }

    // Most text slots per provider call. Each call's strict schema has one required property per slot;
    // ponytail: 100 stays inside every published Structured Outputs object-property limit (100, later 5000);
    // raise it only against the current documented limit. Larger documents go in consecutive chunks.
    public const int MaxSlotsPerRequest = 100;

    // Strict structured output for text-slot requests: exactly the properties SlotKey(0..count-1), all
    // required, each a (non-null) string, nothing else. TranslationTextSlots.ParseResponse re-checks it.
    private static BinaryData TextsSchema(int count)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        for (var i = 0; i < count; i++)
        {
            properties[TranslationTextSlots.SlotKey(i)] = new JsonObject { ["type"] = "string" };
            required.Add(TranslationTextSlots.SlotKey(i));
        }
        return BinaryData.FromString(new JsonObject
        {
            ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false
        }.ToJsonString());
    }

    private static readonly JsonSerializerOptions EnvelopeOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Texts != null)
            return await TranslateTexts(request, cancellationToken);

        // Arbitrary content graphs can't be expressed as a strict JSON schema that also pins protected
        // values (IDs, per-index array items), so the legacy full-document request only forces a JSON
        // object; TranslationOutputValidator enforces the exact contract.
        var (response, failure) = await Complete([new SystemChatMessage(BuildPrompt(request))],
            new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() }, cancellationToken);
        if (failure != null)
            return failure;

        var translatedText = FirstText(response);
        if (string.IsNullOrWhiteSpace(translatedText))
            return Fail(ContentTranslationErrorCodes.EmptyResponse, null, null);

        // Structural validation against the submitted document - not just "is this valid JSON?"
        // but "did the model actually follow the prompt's own contract?" (no added/missing/
        // renamed fields, protected fields byte-for-byte unchanged, translatable fields still
        // string/null with HTML structure intact).
        var validationError = TranslationOutputValidator.Validate(request.ContentJson, translatedText, request.TranslatableFields);
        if (validationError != null)
        {
            var code = validationError == TranslationOutputValidator.InvalidJsonError
                ? ContentTranslationErrorCodes.InvalidJson
                : ContentTranslationErrorCodes.InvalidStructure;
            _logger.LogWarning("Translation failed with {FailureKind}", code);
            return TranslationResult.Failed(code, validationError);
        }

        return TranslationResult.Ok(translatedText, ProviderName, response.Model ?? _model);
    }

    // Only the target language and the ordered values: no IDs, structure or markup. Chunks go one after
    // another; the first failed chunk fails the whole request (nothing partial is returned or resent).
    private async Task<TranslationResult> TranslateTexts(TranslationRequest request, CancellationToken cancellationToken)
    {
        var language = request.TargetLanguage ?? "Persian (Farsi)";
        var translations = new List<string>(request.Texts.Count);
        string model = null;
        foreach (var chunk in request.Texts.Chunk(MaxSlotsPerRequest))
        {
            // Per call: the SDK writes the messages/model into the options instance.
            var (response, failure) = await Complete(
                [
                    new SystemChatMessage(BuildTextsPrompt(language)),
                    new UserChatMessage(JsonSerializer.Serialize(new { targetLanguage = language, texts = chunk }, EnvelopeOptions))
                ],
                new ChatCompletionOptions
                {
                    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("translations", TextsSchema(chunk.Length), jsonSchemaIsStrict: true)
                },
                cancellationToken);
            if (failure != null)
                return failure;

            var translatedText = FirstText(response);
            if (string.IsNullOrWhiteSpace(translatedText))
                return Fail(ContentTranslationErrorCodes.EmptyResponse, null, null);

            var slotError = TranslationTextSlots.ParseResponse(translatedText, chunk.Length, out var values, out var shape);
            if (slotError != null)
            {
                _logger.LogWarning("Translation failed with {FailureKind}", slotError);
                return TranslationResult.Failed(slotError, responseShape: shape);
            }
            translations.AddRange(values);
            model = response.Model ?? _model;
        }
        return TranslationResult.OkTexts(translations, ProviderName, model);
    }

    // One provider call, never resent: the completion, or a classified failure.
    private async Task<(ChatCompletion Response, TranslationResult Failure)> Complete(List<ChatMessage> messages, ChatCompletionOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await _client.CompleteChatAsync(messages, options, cancellationToken), null);
        }
        catch (OperationCanceledException)
        {
            // Shutdown or NetworkTimeout: ambiguous, the caller records it without resending.
            throw;
        }
        catch (ClientResultException ex) when (ex.Status != 0)
        {
            return (null, Fail(ClassifyStatus(ex.Status), ex.Status, ex, ex.Status is 429 or 503 ? RetryAfter(ex) : null));
        }
        catch (Exception ex)
        {
            // Only a failure to connect proves no response was received; it still stays terminal
            // since the request may have been partly sent. Anything else is unknown.
            var code = IsConnectFailure(ex) || IsConnectFailure(ex.InnerException)
                ? ContentTranslationErrorCodes.ProviderNetwork
                : ContentTranslationErrorCodes.ProviderError;
            return (null, Fail(code, null, ex));
        }
    }

    // The completion can come back with no choices or no content parts (e.g. a refusal); the SDK's
    // Content getter itself indexes choices[0] and throws when there are none.
    private static string FirstText(ChatCompletion response)
    {
        try
        {
            return response.Content is { Count: > 0 } content ? content[0].Text : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // 429 and 503 are documented as rejected before processing; 408 and other 5xx may have been
    // processed (and billed), so they are unknown rather than transient.
    private static string ClassifyStatus(int status) => status switch
    {
        429 => ContentTranslationErrorCodes.ProviderRateLimited,
        503 => ContentTranslationErrorCodes.ProviderTransient,
        408 => ContentTranslationErrorCodes.ProviderError,
        >= 400 and < 500 => ContentTranslationErrorCodes.ProviderRejected,
        _ => ContentTranslationErrorCodes.ProviderError
    };

    private static bool IsConnectFailure(Exception ex) =>
        ex is HttpRequestException
        {
            HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError
                or HttpRequestError.SecureConnectionError or HttpRequestError.ProxyTunnelError
        };

    private static TimeSpan? RetryAfter(ClientResultException ex)
    {
        if (ex.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var value) != true
            || !RetryConditionHeaderValue.TryParse(value, out var retry))
            return null;
        var delay = retry.Delta ?? retry.Date - DateTimeOffset.UtcNow;
        return delay > TimeSpan.Zero ? delay : null;
    }

    private TranslationResult Fail(string code, int? status, Exception ex, TimeSpan? retryAfter = null)
    {
        _logger.LogWarning("Translation failed with {FailureKind} (HTTP {HttpStatus}, {ExceptionType})", code, status, ex?.GetType().Name);
        return TranslationResult.Failed(code, retryAfter: retryAfter, httpStatus: status, exceptionType: ex?.GetType().Name);
    }

    private static string BuildTextsPrompt(string language) => $"""
        You are a professional {language} translator for a luxury international magazine on fashion, design, art, architecture,
        culture and lifestyle. Translate with precision, cultural nuance and natural, fluent {language}.

        The user message is a JSON object with "targetLanguage" and "texts", an ordered array of text values.
        Translate every value in "texts" into {language}, even if it looks like sample, draft or placeholder text.
        Keep technical tokens (such as H1-H6), numbers and proper brand names as they are.
        Respond with only a JSON object with one required property per input value: "v0" is the translation of the
        first value, "v1" of the second, and so on. Return every property, each a non-empty translated string.
        No other properties, no markdown, no commentary.
        """;

    private static string BuildPrompt(TranslationRequest request)
    {
        var language = request.TargetLanguage ?? "Persian (Farsi)";
        var fields = string.Join("\n", (request.TranslatableFields ?? TranslationOutputValidator.DefaultTranslatableFields)
            .Select(f => $"                    - {f}"));
        return BuildPrompt(request.ContentJson, language, fields);
    }

    private static string BuildPrompt(string contentJson, string language, string translatableFields) => $@"
                    You are a professional {language} translator specializing in content for a luxury international magazine focused on fashion,
                    design, art, culture and lifestyle. You have expert knowledge of all terminology and specialized expressions in fashion, design,
                    architecture, art, and lifestyle, and you translate texts with precision, cultural nuance, and a high level of fluency for a {language}-speaking audience.

                    Translate all English user-facing text values in the provided JSON object into {language}, while preserving the JSON structure exactly.

                    STRICT RULES:
                    1. Return ONLY valid JSON.
                    2. Do NOT add explanations.
                    3. Do NOT wrap the output in markdown.
                    4. Do NOT change property names.
                    5. Do NOT remove, add, rename, or reorder fields.
                    6. Do NOT modify numbers, IDs, dates, null values, booleans, or non-text values.

                    TRANSLATE ONLY THESE FIELDS:
{translatableFields}

                    DO NOT TRANSLATE THESE FIELDS:
                    - ElementTitle
                    - FileNameText
                    - GalleryImages
                    - Categories
                    - Tags
                    - Cultures
                    - PublishDt
                    - ApplicationId
                    - TypeId
                    - Id
                    - ContentId
                    - SectionId
                    - ElementType
                    - Size
                    - Status
                    - IsDeleted
                    - IsActive
                    - UpdatedDT
                    - CreatedDT
                    - ImageFileName

                    IMPORTANT:
                    7. Translate all English text inside the allowed fields, even if the text looks like sample, test, draft, or placeholder content.
                    8. Preserve technical tokens such as H1, H2, H3, H4, H5, H6 exactly as they are.
                    9. If a field is null, leave it null.

                    HTML RULES:
                    10. If a value contains HTML, preserve the HTML structure exactly.
                    11. Translate only the visible text inside the HTML.
                    12. Do NOT modify HTML tags, attributes, href, rel, target, strong, p, or br tags.
                    13. Preserve empty tags exactly, including <p><br></p>.

                    STYLE RULES:
                    14. Use fluent and natural {language}.
                    15. Avoid awkward literal translation where possible, but keep the meaning accurate.

                    FINAL CHECK:
                    - Translate TinyText and EditorText too.
                    - Keep ElementTitle unchanged.
                    - Preserve HTML exactly.
                    - Return only JSON.

                    JSON:
                    {contentJson}
                    ";
}
