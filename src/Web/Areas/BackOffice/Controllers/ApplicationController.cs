namespace Web.Areas.BackOffice.Controllers;

[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
// Every action here is either about selecting a tenant or administering the tenant root itself -
// none of them consume the session's selected tenant, so none needs one to already be present.
[SkipTenantContextCheck]
public class ApplicationController : BaseController
{
    // fields
    private readonly IApplicationServices _applicationServices;
    private readonly ILogger<ApplicationController> _logger;
    private readonly IHostEnvironment _appEnvironment;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IUserManagementServices _userManagementServices;
    private readonly IFileUploadService _fileUploadService;
    private readonly CodeGenerator _codeGenerator;

    // constructor
    public ApplicationController(ILogger<ApplicationController> logger, IApplicationServices applicationServices,
        IHostEnvironment appEnvironment, UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, IUserManagementServices userManagementServices,
        IFileUploadService fileUploadService, CodeGenerator codeGenerator)
    {
        _applicationServices = applicationServices;
        _logger = logger;
        _appEnvironment = appEnvironment;
        _userManager = userManager;
        _signInManager = signInManager;
        _userManagementServices = userManagementServices;
        _fileUploadService = fileUploadService;
        _codeGenerator = codeGenerator;
    }

    // methods
    public async Task<IActionResult> SelectApp()
    {
        if (!await CheckUserApproval())
            return Redirect("/WaitingForApproval");

        ViewData["UserApplications"] = await _applicationServices.GetUserApplications(User.Identity.Name);

        return View(_applicationServices.List());
    }

    public IActionResult ApplicationForm()
    {
        return View(new ApplicationDto
        {
            LogoFileName = Guid.NewGuid().ToString()
        });
    }

    // Application is the tenant root, not a self-scoped resource - authentication alone is not
    // enough to create one. Only a SuperAdmin may.
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> SaveApplicationForm(ApplicationDto applicationForm)
    {
        applicationForm.IsActive = true;
        var application = await _applicationServices.Create(applicationForm);

        return Content("Done|" + application.Id);
    }

    // EntityId is caller-controlled and would otherwise let any authenticated member overwrite
    // the logo and regenerate the application key for an arbitrary application. [Authorize] runs
    // before this method body, so the SuperAdmin check happens before EntityId is ever looked up.
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> UploadApplicationLogo(IFormFile File, int EntityId)
    {
        var application = await _applicationServices.GetById(EntityId);

        application.ApplicationKey = _codeGenerator.GenerateAppKey(application.Id);

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Application/Logos/");
        var baseName = Path.GetFileNameWithoutExtension(application.LogoFileName);

        var uploadResult = await _fileUploadService.SaveImageAsync(File, savePath, baseName);
        if (!uploadResult.Succeeded)
            return Content("Failed");

        application.LogoFileName = uploadResult.FileName;
        await _applicationServices.Update(application);

        return Content("Done");
    }

    // Selecting an application changes the caller's server-side session state, so this must be a
    // state-changing POST with a CSRF token - not a plain GET link a page could trigger silently.
    [HttpPost]
    [Route("/BackOffice/Application/SelectAppToEnter/{applicationId}")]
    public async Task<IActionResult> SelectAppToEnter(int applicationId)
    {
        if (!await CheckUserApproval())
            return Redirect("/WaitingForApproval");

        try
        {
            await _userManagementServices.SetCurrentApplicationId(User.Identity.Name, applicationId);
        }
        catch (KeyNotFoundException)
        {
            // Core rejected the selection (missing/unauthorized/deleted application or
            // membership - deliberately indistinguishable). Send the user back to their real,
            // legitimate list rather than surfacing an error.
            return Redirect("/BackOffice/Application/SelectApp");
        }

        return Redirect("/BackOffice/Home/Index");
    }

    private async Task<bool> CheckUserApproval()
    {
        var user = await _userManager.FindByEmailAsync(User.Identity.Name);

        return user.IsApprove;
    }

    [Route("/WaitingForApproval")]
    public IActionResult WaitingForApproval()
    {
        return View();
    }

    [Route("/LogoutApp")]
    public async Task<IActionResult> Logout()
    {
        // HttpContext.Session.Remove("AppKey");
        // Response.Cookies.Delete("AppKey");
        // Response.Cookies.Delete("UserIsApprove");
        await _userManagementServices.SetCurrentApplicationId(User.Identity.Name, 0);
        await _signInManager.SignOutAsync();
        return Redirect("/Login");
    }
}
