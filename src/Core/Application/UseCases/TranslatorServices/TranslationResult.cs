namespace Application.UseCases.TranslatorServices;

// Structured success/error result for a translation call, so a failure (bad model output,
// upstream error) is something a caller can inspect instead of only ever an exception.
public class TranslationResult
{
    public bool Success { get; }
    public string TranslatedJson { get; }
    public string Error { get; }

    private TranslationResult(bool success, string translatedJson, string error)
    {
        Success = success;
        TranslatedJson = translatedJson;
        Error = error;
    }

    public static TranslationResult Ok(string translatedJson) => new(true, translatedJson, null);
    public static TranslationResult Failed(string error) => new(false, null, error);
}
