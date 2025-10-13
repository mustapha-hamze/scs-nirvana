using SkiaSharp;

namespace Web.Areas.BackOffice.Controllers;

[Area("BackOffice")]
public class BaseController : Controller
{
    public BaseController()
    {

    }

    public static async Task<bool> UploadImage(IFormFile file, string savePath, string fileName)
    {
        if (file == null || file.Length == 0)
            return false;

        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        var fullPath = Path.Combine(savePath, fileName);

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        using var inputStream = new SKManagedStream(memoryStream);
        using var codec = SKCodec.Create(inputStream);

        if (codec == null)
            return false; // Not a valid image

        var info = codec.Info;

        // Load image
        using var original = SKBitmap.Decode(codec);

        SKBitmap finalImage;

        // Resize if needed
        if (original.Width > 1367)
        {
            int newWidth = original.Width / 2;
            int newHeight = original.Height / 2;

            finalImage = new SKBitmap(newWidth, newHeight);
            using var canvas = new SKCanvas(finalImage);
            canvas.DrawBitmap(original, new SKRect(0, 0, newWidth, newHeight));
        }
        else
        {
            finalImage = original.Copy();
        }

        // Save as same format as input
        var imageFormat = GetImageFormat(codec.EncodedOrigin);

        using var image = SKImage.FromBitmap(finalImage);
        using var data = image.Encode(imageFormat, 90); // 90 = quality

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        data.SaveTo(fileStream);

        return true;
    }

    private static SKEncodedImageFormat GetImageFormat(SKEncodedOrigin origin)
    {
        // Default to JPEG
        return SKEncodedImageFormat.Jpeg;
    }

    public async Task<bool> UploadFile(IFormFile file, string savePath, string fileName)
    {
        if (file.Length == 0)
            return false;

        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        var filePath = Path.Combine(savePath, fileName);

        using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        return true;
    }
}
