using Web.Areas.BackOffice.Features.Content.ViewModels;

namespace Web.Areas.BackOffice.Controllers;

// Uploads/images: content body images/files/galleries used by the section editor, and the
// content's own responsive image variants.
public partial class ContentController
{
    private static readonly string[] AllowedBodyFileExtensions =
        { "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "zip", "csv", "txt" };

    [HttpGet]
    [RequireAccess(AccessKeys.Content.PreviewImages)]
    [Route("/{area}/Content/ContentImages/{contentId}")]
    public async Task<IActionResult> ContentImages(int contentId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var aspectRatio = await _applicationServices.GetApplicationSetting(currentApplicationId, 1001);
        var sizes = await _applicationServices.GetApplicationSetting(currentApplicationId, 1000);
        var images = await _contentServices.GetAllContentImages(contentId, currentApplicationId);
        var access = await _shellContext.GetAccessSnapshotAsync();
        var canUploadImages = access.CanAccess(AccessKeys.Content.UploadImages);

        return View(new ContentImagesViewModel
        {
            ContentImageAspectRatio = aspectRatio,
            ContentImageSizes = sizes,
            ContentImages = images,
            CanUploadImages = canUploadImages,
        });
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Content.SaveBody)]
    public async Task<IActionResult> UploadBodyImage(IFormFile File)
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Images/");

        var uploadResult = await _fileUploadService.SaveImageAsync(File, savePath, Guid.NewGuid().ToString());

        return uploadResult.Succeeded ? Content("Done|" + uploadResult.FileName) : Content("Failed");
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Content.SaveBody)]
    public async Task<IActionResult> UploadBodyFile(IFormFile File, string FileName)
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Files/");

        var uploadResult = await _fileUploadService.SaveFileAsync(File, savePath, FileName, AllowedBodyFileExtensions);

        return uploadResult.Succeeded ? Content("Done|" + uploadResult.FileName) : Content("Failed");
    }

    // All-or-nothing: content.js's uploadBodyImageGallery has no per-file error display, so a
    // silently-dropped failure would leave the caller believing every selected image was saved.
    // An empty selection and any single file's failure both fail the whole batch; only files this
    // call itself wrote are removed on failure, matching SaveImageVariantsAsync's atomicity.
    [HttpPost]
    [RequireAccess(AccessKeys.Content.SaveBody)]
    public async Task<IActionResult> UploadBodyImageGallery()
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Gallery/");

        var uploadedImages = Request.Form.Files;
        if (uploadedImages.Count == 0)
            return Content("Failed");

        var savedFileNames = new List<string>();
        foreach (var item in uploadedImages)
        {
            var uploadResult = await _fileUploadService.SaveImageAsync(item, savePath, Guid.NewGuid().ToString());
            if (!uploadResult.Succeeded)
            {
                foreach (var fileName in savedFileNames)
                {
                    var path = Path.Combine(savePath, fileName);
                    try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }

                return Content("Failed");
            }

            savedFileNames.Add(uploadResult.FileName!);
        }

        return Content("Done|" + string.Join(",", savedFileNames) + ",");
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Content.UploadImages)]
    public async Task<IActionResult> UploadContentImage(IFormFile file, int contentId, int settingId)
    {
        if (file == null || file.Length == 0)
            return Content("Failed");

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Content/Image/" + contentId);

        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var imageSettings = await _applicationServices.GetApplicationSetting(currentApplicationId, 1000);
        var currentImageSettings = imageSettings.SingleOrDefault(s => s.Id == settingId);
        if (currentImageSettings == null)
            return Content("Failed");

        List<(int Width, int Height)> targetSizes;
        try
        {
            targetSizes = currentImageSettings.Value.Split(",").Select(item =>
            {
                var sizes = item.Split("-");
                return (Width: int.Parse(sizes[0]), Height: int.Parse(sizes[1]));
            }).ToList();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or IndexOutOfRangeException)
        {
            return Content("Failed");
        }

        var uploadResult = await _fileUploadService.SaveImageVariantsAsync(file, savePath, targetSizes);
        if (!uploadResult.Succeeded)
            return Content("Failed");

        // Only remove the previous images/records once the new ones have been validated and written successfully.
        await _contentServices.DeleteAllContentImages(contentId, currentApplicationId);
        if (Directory.Exists(savePath))
        {
            var newFileNames = uploadResult.Variants.Select(v => v.FileName).ToHashSet();
            foreach (var existingFile in Directory.GetFiles(savePath))
            {
                if (!newFileNames.Contains(Path.GetFileName(existingFile)))
                    System.IO.File.Delete(existingFile);
            }
        }

        foreach (var variant in uploadResult.Variants)
        {
            await _contentServices.CreateContentImage(new ContentImageDto
            {
                ContentId = contentId,
                ImageFileName = variant.FileName,
                IsActive = true,
                Size = variant.Width
            }, currentApplicationId);
        }

        return Content("Done,");
    }
}
