using System.Collections.Generic;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace Core.Services.TranslatorServices;

public class ContentTranslator : IContentTranslator
{
    private readonly ChatClient _client;

    public ContentTranslator(IConfiguration configuration)
    {
        _client = new ChatClient("gpt-5.6-sol", configuration["OPENAI_API_KEY"]);
    }
    public async Task<string> Translate(Content content)
    {
        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };
        var contentStr = JsonConvert.SerializeObject(content, settings);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($@"
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
                    {contentStr}
                    ")
        };

        var response = await _client.CompleteChatAsync(messages);
        var result = response.Value.Content[0].Text;

        return result;
    }
}