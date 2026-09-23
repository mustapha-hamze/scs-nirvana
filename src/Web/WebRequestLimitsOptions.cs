using Microsoft.Extensions.Options;

namespace Web;

// Bounds form/multipart parsing and file-upload buffering so a client can't force the server to
// hold an unbounded field count/length or image pixel buffer in memory - the prior
// int.MaxValue ValueCountLimit/ValueLengthLimit in AddWebInfrastructure were a DoS vector.
// ValueCountLimit/ValueLengthLimitBytes are sized from this app's actual BackOffice forms (the
// largest, ContentDto, binds under two dozen scalar/collection fields - ASP.NET Core's own
// defaults comfortably cover it); MultipartBodyLengthLimitBytes keeps the already-established
// 60 MB upload ceiling; MaxImagePixelCount rejects a "pixel bomb" (a small file that decompresses
// into a huge bitmap) before FileUploadService allocates it.
public sealed class WebRequestLimitsOptions
{
    public const string SectionName = "WebRequestLimits";

    public int ValueCountLimit { get; set; } = 1024;
    public int ValueLengthLimitBytes { get; set; } = 4 * 1024 * 1024;
    public long MultipartBodyLengthLimitBytes { get; set; } = 60_000_000;
    public long MaxImagePixelCount { get; set; } = 64_000_000;
}

// Fails fast (ValidateOnStart) instead of silently accepting a zero/negative limit that would
// reject every request or upload.
public sealed class WebRequestLimitsOptionsValidator : IValidateOptions<WebRequestLimitsOptions>
{
    public ValidateOptionsResult Validate(string name, WebRequestLimitsOptions options)
    {
        if (options.ValueCountLimit < 1)
            return ValidateOptionsResult.Fail($"{WebRequestLimitsOptions.SectionName}:ValueCountLimit must be at least 1.");
        if (options.ValueLengthLimitBytes < 1)
            return ValidateOptionsResult.Fail($"{WebRequestLimitsOptions.SectionName}:ValueLengthLimitBytes must be at least 1.");
        if (options.MultipartBodyLengthLimitBytes < 1)
            return ValidateOptionsResult.Fail($"{WebRequestLimitsOptions.SectionName}:MultipartBodyLengthLimitBytes must be at least 1.");
        if (options.MaxImagePixelCount < 1)
            return ValidateOptionsResult.Fail($"{WebRequestLimitsOptions.SectionName}:MaxImagePixelCount must be at least 1.");

        return ValidateOptionsResult.Success;
    }
}
