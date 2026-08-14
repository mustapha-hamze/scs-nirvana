#nullable enable
namespace Web.Services.FileUpload;

public sealed class FileUploadResult
{
    public bool Succeeded { get; }
    public string? FileName { get; }
    public string? Error { get; }

    private FileUploadResult(bool succeeded, string? fileName, string? error)
    {
        Succeeded = succeeded;
        FileName = fileName;
        Error = error;
    }

    public static FileUploadResult Success(string fileName) => new(true, fileName, null);
    public static FileUploadResult Failure(string error) => new(false, null, error);
}

public sealed record ImageVariant(int Width, int Height, string FileName);

public sealed class ImageVariantsUploadResult
{
    public bool Succeeded { get; }
    public IReadOnlyList<ImageVariant> Variants { get; }
    public string? Error { get; }

    private ImageVariantsUploadResult(bool succeeded, IReadOnlyList<ImageVariant> variants, string? error)
    {
        Succeeded = succeeded;
        Variants = variants;
        Error = error;
    }

    public static ImageVariantsUploadResult Success(IReadOnlyList<ImageVariant> variants) => new(true, variants, null);
    public static ImageVariantsUploadResult Failure(string error) => new(false, Array.Empty<ImageVariant>(), error);
}

public enum ImageOutputFormat
{
    /// <summary>Keep the format the uploaded image actually decodes as.</summary>
    PreserveOriginal,
    Jpeg
}
