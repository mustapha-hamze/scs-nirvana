// UserAttachmentController
namespace Web.Areas.BackOffice.Controllers;
[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class UserAttachmentController : BaseController
{
    private readonly ISender _sender;
    private readonly IHostEnvironment _appEnvironment;
    public UserAttachmentController(ISender sender, IHostEnvironment appEnvironment)
    {
        _sender = sender;
        _appEnvironment = appEnvironment;
    }
    [HttpGet("/{area}/{controller}/UserAttachmentForm/{userId}/{id?}")]
    public async Task<IActionResult> UserAttachmentForm(string userId, int id = 0)
    {
        if (id != 0)
        {
            var attachment = await _sender.Send(new GetUserAttachmentByIdQuery(id));
            return View(attachment);
        }
        return View(new UserAttachmentDto { UserId = userId });
    }

    [HttpGet("/{area}/{controller}/UserAttachmentsList/{userId?}")]
    public async Task<IActionResult> UserAttachmentsList(string userId = "")
    {
        var attachments = await _sender.Send(new GetUserAttachmentsQuery(userId));
        return View(attachments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HttpGet("/{area}/{controller}/SaveUserAttachment")]
    public async Task<IActionResult> SaveUserAttachment(UserAttachmentDto attachment)
    {
        await _sender.Send(new CreateUserAttachmentCommand(attachment));
        return Content("Done");
    }

    [HttpPost]
    public IActionResult UploadAttachmentFile(IFormFile file, string userId, int attachmentId, string attachmentType)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is not selected or empty.");
        }

        var fileExtension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(fileExtension) || !fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid file type. Only PDF files are allowed.");
        }
        //TODO: Implement Realistic Implementation
        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/UserAttachment/" + userId);
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        // Ensure the file name has a .pdf extension
        var pdfFileName = attachmentType + "_" + attachmentId + ".pdf";
        var filePath = Path.Combine(savePath, pdfFileName);

        // Save the PDF file to the specified location
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            file.CopyTo(stream);
        }

        return Ok();
    }
}