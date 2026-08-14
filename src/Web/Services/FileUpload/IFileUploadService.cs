namespace Web.Services.FileUpload;

public interface IFileUploadService
{
    /// <summary>
    /// Validates that the uploaded file decodes as an image and saves it under <paramref name="directory"/>.
    /// The output extension matches the image's real, decoded format (or <see cref="ImageOutputFormat.Jpeg"/>
    /// when forced), never the caller-supplied extension or content type.
    /// </summary>
    Task<FileUploadResult> SaveImageAsync(IFormFile file, string directory, string fileNameWithoutExtension,
        ImageOutputFormat outputFormat = ImageOutputFormat.PreserveOriginal);

    /// <summary>
    /// Decodes the uploaded image once and saves a resized variant per requested size, each under a
    /// freshly generated file name. Fails atomically: if any size cannot be produced, nothing is written
    /// to disk that the caller needs to clean up, since callers should only remove prior files once this
    /// returns success.
    /// </summary>
    Task<ImageVariantsUploadResult> SaveImageVariantsAsync(IFormFile file, string directory,
        IEnumerable<(int Width, int Height)> sizes);

    /// <summary>
    /// Saves a generic (non-image) file after checking its extension against <paramref name="allowedExtensions"/>.
    /// </summary>
    Task<FileUploadResult> SaveFileAsync(IFormFile file, string directory, string fileNameWithoutExtension,
        IReadOnlyCollection<string> allowedExtensions);
}
