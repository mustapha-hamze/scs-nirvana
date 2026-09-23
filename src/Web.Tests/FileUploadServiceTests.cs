using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Web.Services.FileUpload;
using Xunit;

namespace Web.Tests;

// Web Phase 5 task 1: FileUploadService's own request/upload robustness - a finite byte limit
// enforced before any buffering/decoding, a pixel-count ceiling checked before SKBitmap.Decode
// allocates, single-step proportional resizing, and SaveImageVariantsAsync's atomic cleanup on
// partial failure. Each test uses a fresh temp directory so filesystem assertions are isolated.
public sealed class FileUploadServiceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("scs-file-upload-tests-").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static FileUploadService CreateService(long maxFileBytes = 60_000_000, long maxImagePixelCount = 64_000_000)
    {
        var options = new WebRequestLimitsOptions
        {
            MultipartBodyLengthLimitBytes = maxFileBytes,
            MaxImagePixelCount = maxImagePixelCount,
        };
        return new FileUploadService(Options.Create(options));
    }

    private static IFormFile MakeFormFile(byte[] content, string fileName = "upload.bin")
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName);
    }

    private static byte[] EncodeImage(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(SKColors.CornflowerBlue);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    // ---- SaveFileAsync (generic files) ----

    [Fact]
    public async Task SaveFileAsync_EmptyFile_ReturnsFailure_WithoutWritingAnyFile()
    {
        var service = CreateService();
        var file = MakeFormFile(Array.Empty<byte>(), "empty.pdf");

        var result = await service.SaveFileAsync(file, _directory, "doc", new[] { "pdf" });

        Assert.False(result.Succeeded);
        Assert.Empty(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SaveFileAsync_OversizeFile_ReturnsFailure_WithoutWritingFile()
    {
        var service = CreateService(maxFileBytes: 10);
        var file = MakeFormFile(Encoding.UTF8.GetBytes("this content is longer than ten bytes"), "doc.pdf");

        var result = await service.SaveFileAsync(file, _directory, "doc", new[] { "pdf" });

        Assert.False(result.Succeeded);
        Assert.False(Directory.Exists(_directory) && Directory.GetFiles(_directory).Any());
    }

    [Fact]
    public async Task SaveFileAsync_DisallowedExtension_ReturnsFailure_WithoutWritingFile()
    {
        var service = CreateService();
        var file = MakeFormFile(Encoding.UTF8.GetBytes("payload"), "script.exe");

        var result = await service.SaveFileAsync(file, _directory, "doc", new[] { "pdf" });

        Assert.False(result.Succeeded);
        Assert.False(Directory.Exists(_directory) && Directory.GetFiles(_directory).Any());
    }

    [Fact]
    public async Task SaveFileAsync_AllowedExtension_WritesFile()
    {
        var service = CreateService();
        var file = MakeFormFile(Encoding.UTF8.GetBytes("%PDF-1.4 fake pdf body"), "doc.pdf");

        var result = await service.SaveFileAsync(file, _directory, "doc", new[] { "pdf" });

        Assert.True(result.Succeeded);
        Assert.Equal("doc.pdf", result.FileName);
        Assert.True(File.Exists(Path.Combine(_directory, "doc.pdf")));
    }

    // ---- SaveImageAsync (images) ----

    [Fact]
    public async Task SaveImageAsync_InvalidImageBytes_ReturnsFailure_WithoutWritingFile()
    {
        var service = CreateService();
        var file = MakeFormFile(Encoding.UTF8.GetBytes("not an image"), "fake.png");

        var result = await service.SaveImageAsync(file, _directory, "img");

        Assert.False(result.Succeeded);
        Assert.False(Directory.Exists(_directory) && Directory.GetFiles(_directory).Any());
    }

    [Fact]
    public async Task SaveImageAsync_PixelBomb_ReturnsFailure_WithoutWritingFile()
    {
        // A real, small-on-disk 40x40 image (1600 px) rejected by a deliberately tiny pixel cap -
        // proves the guard fires from the codec header, not from a manufactured huge file.
        var service = CreateService(maxImagePixelCount: 100);
        var bytes = EncodeImage(40, 40);
        var file = MakeFormFile(bytes, "big.png");

        var result = await service.SaveImageAsync(file, _directory, "img");

        Assert.False(result.Succeeded);
        Assert.False(Directory.Exists(_directory) && Directory.GetFiles(_directory).Any());
    }

    [Fact]
    public async Task SaveImageAsync_WiderThanMax_ResizesProportionally()
    {
        var service = CreateService();
        // 3000x1500 (2:1) is well past the service's internal 1367px max width.
        var bytes = EncodeImage(3000, 1500);
        var file = MakeFormFile(bytes, "wide.png");

        var result = await service.SaveImageAsync(file, _directory, "img");

        Assert.True(result.Succeeded);
        using var savedBitmap = SKBitmap.Decode(Path.Combine(_directory, result.FileName!));
        Assert.Equal(1367, savedBitmap.Width);
        Assert.Equal(684, savedBitmap.Height); // proportional: 1500 * (1367/3000), rounded
    }

    [Fact]
    public async Task SaveImageAsync_NarrowerThanMax_IsNotUpscaled()
    {
        var service = CreateService();
        var bytes = EncodeImage(200, 100);
        var file = MakeFormFile(bytes, "small.png");

        var result = await service.SaveImageAsync(file, _directory, "img");

        Assert.True(result.Succeeded);
        using var savedBitmap = SKBitmap.Decode(Path.Combine(_directory, result.FileName!));
        Assert.Equal(200, savedBitmap.Width);
        Assert.Equal(100, savedBitmap.Height);
    }

    // ---- SaveImageVariantsAsync (atomicity) ----

    [Fact]
    public async Task SaveImageVariantsAsync_AllSucceed_WritesEveryVariant()
    {
        var service = CreateService();
        var bytes = EncodeImage(400, 400);
        var file = MakeFormFile(bytes, "src.png");

        var result = await service.SaveImageVariantsAsync(file, _directory, new[] { (100, 100), (50, 50) });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Variants.Count);
        Assert.Equal(2, Directory.GetFiles(_directory).Length);
    }

    [Fact]
    public async Task SaveImageVariantsAsync_PartialFailure_CleansUpWrittenFiles_AndReportsFailure()
    {
        var service = CreateService();
        var bytes = EncodeImage(400, 400);
        var file = MakeFormFile(bytes, "src.png");

        // First size succeeds and writes a file; the second is invalid and must fail the whole call.
        var result = await service.SaveImageVariantsAsync(file, _directory, new[] { (100, 100), (0, 0) });

        Assert.False(result.Succeeded);
        Assert.Empty(result.Variants);
        Assert.Empty(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SaveImageVariantsAsync_PartialFailure_DoesNotDeletePreExistingFiles()
    {
        var service = CreateService();
        var preExisting = Path.Combine(_directory, "pre-existing.txt");
        File.WriteAllText(preExisting, "keep me");

        var bytes = EncodeImage(400, 400);
        var file = MakeFormFile(bytes, "src.png");

        var result = await service.SaveImageVariantsAsync(file, _directory, new[] { (100, 100), (0, 0) });

        Assert.False(result.Succeeded);
        Assert.True(File.Exists(preExisting));
    }

    // Web Phase 5 task 2: invalid dimensions (above) fail before the current variant's target
    // file is even created, so they can't prove cleanup of a partially written file. This uses
    // the internal TestOnlyBeforeVariantWrite seam to fail deterministically after the second
    // variant's target file has been created (FileMode.Create already ran) but before any bytes
    // are written to it - the exact gap the tracked-before-open fix closes.
    [Fact]
    public async Task SaveImageVariantsAsync_FailureAfterFileCreatedButBeforeWrite_RemovesPartialAndPriorFiles_KeepsSentinel()
    {
        var service = CreateService();
        var sentinel = Path.Combine(_directory, "sentinel.txt");
        File.WriteAllText(sentinel, "keep me");

        var bytes = EncodeImage(400, 400);
        var file = MakeFormFile(bytes, "src.png");

        var writeCallCount = 0;
        service.TestOnlyBeforeVariantWrite = _ =>
        {
            writeCallCount++;
            if (writeCallCount == 2)
                throw new IOException("simulated failure after target-file creation, before write");
        };

        var result = await service.SaveImageVariantsAsync(file, _directory, new[] { (100, 100), (50, 50) });

        Assert.False(result.Succeeded);
        Assert.Empty(result.Variants);
        Assert.Equal(2, writeCallCount); // proves the second variant's file was created and the hook actually ran
        var remaining = Directory.GetFiles(_directory);
        Assert.Equal(new[] { sentinel }, remaining);
    }
}
