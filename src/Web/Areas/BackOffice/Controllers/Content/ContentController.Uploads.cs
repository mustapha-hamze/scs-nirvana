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
        ViewData["ContentImageAspectRatio"] = await _applicationServices.GetApplicationSetting(currentApplicationId, 1001);
        ViewData["ContentImageSizes"] = await _applicationServices.GetApplicationSetting(currentApplicationId, 1000);
        ViewData["ContentImage"] = await _contentServices.GetAllContentImages(contentId, currentApplicationId);
        return View();
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

    [HttpPost]
    [RequireAccess(AccessKeys.Content.SaveBody)]
    public async Task<IActionResult> UploadBodyImageGallery()
    {
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Section/Gallery/");

        string result = string.Empty;
        var uploadedImages = Request.Form.Files;
        foreach (var item in uploadedImages)
        {
            var uploadResult = await _fileUploadService.SaveImageAsync(item, savePath, Guid.NewGuid().ToString());
            if (uploadResult.Succeeded)
                result += uploadResult.FileName + ",";
        }

        return Content("Done|" + result);
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
        var currentImageSettings = imageSettings.Single(s => s.Id == settingId);

        var targetSizes = currentImageSettings.Value.Split(",").Select(item =>
        {
            var sizes = item.Split("-");
            return (Width: Convert.ToInt32(sizes[0]), Height: Convert.ToInt32(sizes[1]));
        });

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
