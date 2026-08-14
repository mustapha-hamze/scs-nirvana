#nullable enable
using System.Diagnostics.CodeAnalysis;
using SkiaSharp;

namespace Web.Services.FileUpload;

public sealed class FileUploadService : IFileUploadService
{
    private const int MaxImageWidth = 1367;
    private const int ImageQuality = 90;

    private static readonly IReadOnlyDictionary<SKEncodedImageFormat, string> SupportedImageFormats =
        new Dictionary<SKEncodedImageFormat, string>
        {
            [SKEncodedImageFormat.Jpeg] = "jpg",
            [SKEncodedImageFormat.Png] = "png",
            [SKEncodedImageFormat.Gif] = "gif",
            [SKEncodedImageFormat.Webp] = "webp",
            [SKEncodedImageFormat.Bmp] = "bmp"
        };

    public async Task<FileUploadResult> SaveImageAsync(IFormFile file, string directory, string fileNameWithoutExtension,
        ImageOutputFormat outputFormat = ImageOutputFormat.PreserveOriginal)
    {
        var decoded = await DecodeImageAsync(file);
        if (!decoded.Succeeded)
            return FileUploadResult.Failure(decoded.Error!);

        using var bitmap = decoded.Bitmap;
        using var final = ResizeIfNeeded(bitmap!, MaxImageWidth);

        var format = outputFormat == ImageOutputFormat.Jpeg ? SKEncodedImageFormat.Jpeg : decoded.Format;
        var extension = outputFormat == ImageOutputFormat.Jpeg ? "jpg" : decoded.Extension;

        if (!TryEncode(final, format, out var data))
            return FileUploadResult.Failure("Unable to encode image.");

        using (data)
        {
            var fileName = $"{SanitizeFileName(fileNameWithoutExtension)}.{extension}";
            var fullPath = ResolveSafePath(directory, fileName);

            Directory.CreateDirectory(directory);
            await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);

            return FileUploadResult.Success(fileName);
        }
    }

    public async Task<ImageVariantsUploadResult> SaveImageVariantsAsync(IFormFile file, string directory,
        IEnumerable<(int Width, int Height)> sizes)
    {
        var decoded = await DecodeImageAsync(file);
        if (!decoded.Succeeded)
            return ImageVariantsUploadResult.Failure(decoded.Error!);

        using var bitmap = decoded.Bitmap;

        var variants = new List<ImageVariant>();
        foreach (var (width, height) in sizes)
        {
            using var resized = bitmap!.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default);
            if (resized is null || !TryEncode(resized, decoded.Format, out var data))
                return ImageVariantsUploadResult.Failure($"Unable to encode image for size {width}x{height}.");

            using (data)
            {
                var fileName = $"{Guid.NewGuid()}.{decoded.Extension}";
                var fullPath = ResolveSafePath(directory, fileName);

                Directory.CreateDirectory(directory);
                await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
                data.SaveTo(stream);

                variants.Add(new ImageVariant(width, height, fileName));
            }
        }

        return ImageVariantsUploadResult.Success(variants);
    }

    public async Task<FileUploadResult> SaveFileAsync(IFormFile file, string directory, string fileNameWithoutExtension,
        IReadOnlyCollection<string> allowedExtensions)
    {
        if (file is null || file.Length == 0)
            return FileUploadResult.Failure("No file was selected or the file is empty.");

        var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return FileUploadResult.Failure("This file type is not allowed.");

        var fileName = $"{SanitizeFileName(fileNameWithoutExtension)}.{extension}";
        var fullPath = ResolveSafePath(directory, fileName);

        Directory.CreateDirectory(directory);
        await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await file.CopyToAsync(stream);

        return FileUploadResult.Success(fileName);
    }

    private sealed record ImageDecodeResult(bool Succeeded, SKBitmap? Bitmap, SKEncodedImageFormat Format, string Extension, string? Error)
    {
        public static ImageDecodeResult Failure(string error) => new(false, null, default, string.Empty, error);
    }

    private static async Task<ImageDecodeResult> DecodeImageAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return ImageDecodeResult.Failure("No file was selected or the file is empty.");

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        // Decodability, not the client-supplied content type or file extension, is what proves this is really
        // an image; the codec's own reported format (not the origin/orientation) drives the output extension.
        using (var codecStream = new SKManagedStream(new MemoryStream(bytes)))
        using (var codec = SKCodec.Create(codecStream))
        {
            if (codec is null || !SupportedImageFormats.TryGetValue(codec.EncodedFormat, out var extension))
                return ImageDecodeResult.Failure("The uploaded file is not a supported image.");

            using var bitmapStream = new MemoryStream(bytes);
            var bitmap = SKBitmap.Decode(bitmapStream);
            if (bitmap is null)
                return ImageDecodeResult.Failure("The uploaded file could not be decoded as an image.");

            return new ImageDecodeResult(true, bitmap, codec.EncodedFormat, extension, null);
        }
    }

    private static SKBitmap ResizeIfNeeded(SKBitmap original, int maxWidth)
    {
        if (original.Width <= maxWidth)
            return original.Copy();

        var newWidth = original.Width / 2;
        var newHeight = original.Height / 2;
        return original.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default);
    }

    private static bool TryEncode(SKBitmap bitmap, SKEncodedImageFormat format, [NotNullWhen(true)] out SKData? data)
    {
        using var image = SKImage.FromBitmap(bitmap);
        data = image.Encode(format, ImageQuality);
        return data is not null;
    }

    private static string SanitizeFileName(string name)
    {
        var fileNameOnly = Path.GetFileName(name);
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileNameOnly.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? Guid.NewGuid().ToString() : cleaned;
    }

    private static string ResolveSafePath(string directory, string fileName)
    {
        var fullDirectory = Path.GetFullPath(directory);
        var fullPath = Path.GetFullPath(Path.Combine(fullDirectory, fileName));

        if (!fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Resolved file path escapes the target directory.");

        return fullPath;
    }
}
