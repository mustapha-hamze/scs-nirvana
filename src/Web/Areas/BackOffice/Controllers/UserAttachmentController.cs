// UserAttachmentController
namespace Web.Areas.BackOffice.Controllers;
[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class UserAttachmentController : BaseController
{
    private readonly ISender _sender;
    private readonly IHostEnvironment _appEnvironment;
    private readonly IFileUploadService _fileUploadService;
    public UserAttachmentController(ISender sender, IHostEnvironment appEnvironment, IFileUploadService fileUploadService)
    {
        _sender = sender;
        _appEnvironment = appEnvironment;
        _fileUploadService = fileUploadService;
    }
    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("/{area}/{controller}/UserAttachmentForm/{userId}/{id?}")]
    public async Task<IActionResult> UserAttachmentForm(string userId, int id = 0)
    {
        if (id != 0)
        {
            var attachment = await _sender.Send(new GetUserAttachmentByIdQuery(id, userId));
            return View(attachment);
        }
        return View(new UserAttachmentDto { UserId = userId });
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("/{area}/{controller}/UserAttachmentsList/{userId?}")]
    public async Task<IActionResult> UserAttachmentsList(string userId = "")
    {
        var attachments = await _sender.Send(new GetUserAttachmentsQuery(userId));
        return View(attachments);
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [Route("/{area}/{controller}/SaveUserAttachment")]
    public async Task<IActionResult> SaveUserAttachment(UserAttachmentDto attachment)
    {
        await _sender.Send(new CreateUserAttachmentCommand(attachment));
        return Content("Done");
    }

    private static readonly string[] AllowedAttachmentExtensions = { "pdf" };

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> UploadAttachmentFile(IFormFile file, string userId, int attachmentId, string attachmentType)
    {
        // Ownership must be proven before any filesystem work: attachmentId is otherwise just a
        // caller-supplied number that could belong to another user's attachment.
        try
        {
            await _sender.Send(new GetUserAttachmentByIdQuery(attachmentId, userId));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/UserAttachment/" + userId);
        var fileName = attachmentType + "_" + attachmentId;

        var uploadResult = await _fileUploadService.SaveFileAsync(file, savePath, fileName, AllowedAttachmentExtensions);
        if (!uploadResult.Succeeded)
            return BadRequest(uploadResult.Error);

        return Ok();
    }
}