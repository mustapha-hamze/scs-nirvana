namespace Application.UseCases.TranslatorServices;

// Structured success/error result for a translation call, so a failure (bad model output,
// upstream error) is something a caller can inspect instead of only ever an exception.
public class TranslationResult
{
    public bool Success { get; }
    public string TranslatedJson { get; }

    // Fixed ContentTranslationErrorCodes value; whether it is retried is decided by
    // ContentTranslationErrorCodes.IsRetryable, never by the port.
    public string FailureCode { get; }

    // Secret-safe detail (field names/paths only) for synchronous callers; defaults to FailureCode.
    // Never stored on a job or shown on the dashboard.
    public string Error { get; }

    // Server-log diagnostics only: provider Retry-After, HTTP status and exception type name.
    public TimeSpan? RetryAfter { get; }
    public int? HttpStatus { get; }
    public string ExceptionType { get; }

    public string Provider { get; }
    public string Model { get; }

    private TranslationResult(bool success, string translatedJson, string failureCode, string error, TimeSpan? retryAfter,
        int? httpStatus, string exceptionType, string provider, string model)
    {
        Success = success;
        TranslatedJson = translatedJson;
        FailureCode = failureCode;
        Error = error ?? failureCode;
        RetryAfter = retryAfter;
        HttpStatus = httpStatus;
        ExceptionType = exceptionType;
        Provider = provider;
        Model = model;
    }

    public static TranslationResult Ok(string translatedJson, string provider = null, string model = null) =>
        new(true, translatedJson, null, null, null, null, null, provider, model);

    public static TranslationResult Failed(string failureCode, string error = null, TimeSpan? retryAfter = null, int? httpStatus = null,
        string exceptionType = null) =>
        new(false, null, failureCode, error, retryAfter, httpStatus, exceptionType, null, null);
}
