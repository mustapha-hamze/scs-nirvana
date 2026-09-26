namespace Application.UseCases.TranslatorServices;

// Structured success/error result for a translation call, so a failure (bad model output,
// upstream error) is something a caller can inspect instead of only ever an exception.
public class TranslationResult
{
    public bool Success { get; }
    public string TranslatedJson { get; }
    public string Error { get; }

    // True only when the provider definitively rejected the request without processing it
    // (e.g. rate limited), so resending can't produce a duplicate charge.
    public bool Retryable { get; }

    public string Provider { get; }
    public string Model { get; }

    private TranslationResult(bool success, string translatedJson, string error, bool retryable, string provider, string model)
    {
        Success = success;
        TranslatedJson = translatedJson;
        Error = error;
        Retryable = retryable;
        Provider = provider;
        Model = model;
    }

    public static TranslationResult Ok(string translatedJson, string provider = null, string model = null) =>
        new(true, translatedJson, null, false, provider, model);

    public static TranslationResult Failed(string error, bool retryable = false) => new(false, null, error, retryable, null, null);
}
