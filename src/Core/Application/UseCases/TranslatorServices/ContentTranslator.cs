using System;
using System.Threading;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Newtonsoft.Json;

namespace Core.Services.TranslatorServices;

// Compatibility adapter: existing Web callers still get Translate(content) -> string exactly as
// before. Internally it now serializes content and delegates to the Application-level
// ITranslationPort instead of calling the OpenAI SDK directly - the concrete provider is an
// Infrastructure concern the caller no longer needs to know about.
public class ContentTranslator : IContentTranslator
{
    private readonly ITranslationPort _translationPort;

    public ContentTranslator(ITranslationPort translationPort)
    {
        _translationPort = translationPort;
    }

    public async Task<string> Translate(Content content, CancellationToken cancellationToken = default)
    {
        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };
        var contentJson = JsonConvert.SerializeObject(content, settings);

        var result = await _translationPort.TranslateAsync(new TranslationRequest(contentJson), cancellationToken);

        if (!result.Success)
            throw new InvalidOperationException(result.Error);

        return result.TranslatedJson;
    }
}
