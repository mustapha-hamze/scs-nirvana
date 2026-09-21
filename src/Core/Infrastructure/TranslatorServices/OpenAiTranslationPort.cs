using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Infrastructure.TranslatorServices;

// The only place the OpenAI SDK is touched. Never logs the API key or full request/response
// content - only failure shapes (exception type, "not valid JSON") make it into TranslationResult.Error.
public class OpenAiTranslationPort : ITranslationPort
{
    private readonly ChatClient _client;

    public OpenAiTranslationPort(IOptions<OpenAiTranslationOptions> options)
    {
        var settings = options.Value;
        _client = new ChatClient(settings.Model, new ApiKeyCredential(settings.ApiKey),
            new OpenAIClientOptions { NetworkTimeout = TimeSpan.FromMinutes(settings.NetworkTimeoutMinutes) });
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage> { new SystemChatMessage(BuildPrompt(request.ContentJson)) };

        ChatCompletion response;
        try
        {
            response = await _client.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TranslationResult.Failed($"Translation request failed: {ex.GetType().Name}");
        }

        var translatedText = response.Content[0].Text;

        // Structural validation against the submitted document - not just "is this valid JSON?"
        // but "did the model actually follow the prompt's own contract?" (no added/missing/
        // renamed fields, protected fields byte-for-byte unchanged, translatable fields still
        // string/null with HTML structure intact).
        var validationError = TranslationOutputValidator.Validate(request.ContentJson, translatedText);
        if (validationError != null)
            return TranslationResult.Failed(validationError);

        return TranslationResult.Ok(translatedText);
    }

    private static string BuildPrompt(string contentJson) => $@"
                    You are a professional Persian (Farsi) translator specializing in content for a luxury international magazine focused on fashion,
                    design, art, culture and lifestyle. You have expert knowledge of all terminology and specialized expressions in fashion, design,
                    architecture, art, and lifestyle, and you translate texts with precision, cultural nuance, and a high level of fluency for a Persian-speaking audience.

                    Translate all English user-facing text values in the provided JSON object into Persian (Farsi), while preserving the JSON structure exactly.

                    STRICT RULES:
                    1. Return ONLY valid JSON.
                    2. Do NOT add explanations.
                    3. Do NOT wrap the output in markdown.
                    4. Do NOT change property names.
                    5. Do NOT remove, add, rename, or reorder fields.
                    6. Do NOT modify numbers, IDs, dates, null values, booleans, or non-text values.

                    TRANSLATE ONLY THESE FIELDS:
                    - Title
                    - HeadLine
                    - Abstract
                    - Description
                    - TinyText
                    - EditorText

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
                    14. Use fluent and natural Persian.
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
