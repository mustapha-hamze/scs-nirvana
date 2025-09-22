namespace Web.Areas.BackOffice.Controllers;
[Area("BackOffice")]
public class BaseController : Microsoft.AspNetCore.Mvc.Controller
{
    public BaseController()
    {

    }

    public bool UploadImage(IFormFile file, string savePath, string fileName)
    {
        if (file.Length == 0)
            return false;

        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        // using var image = Image.Load(file.OpenReadStream());
        // if (image.Width > 1367)
        // {
        //     image.Mutate(x => x.Resize((image.Width / 2), (image.Height / 2)));
        //     image.Save(savePath + fileName);
        // }
        // else
        // {
        //     using (Stream fileStream = new FileStream(savePath + fileName, FileMode.Create))
        //     {
        //         await file.CopyToAsync(fileStream);
        //     }
        // }

        return true;
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
