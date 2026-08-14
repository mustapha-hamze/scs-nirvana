namespace Web.Areas.BackOffice.Controllers;

[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
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

    // constructor
    public ApplicationController(ILogger<ApplicationController> logger, IApplicationServices applicationServices,
        IHostEnvironment appEnvironment, UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, IUserManagementServices userManagementServices,
        IFileUploadService fileUploadService)
    {
        _applicationServices = applicationServices;
        _logger = logger;
        _appEnvironment = appEnvironment;
        _userManager = userManager;
        _signInManager = signInManager;
        _userManagementServices = userManagementServices;
        _fileUploadService = fileUploadService;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveApplicationForm(ApplicationDto applicationForm)
    {
        applicationForm.IsActive = true;
        var application = await _applicationServices.Create(applicationForm);

        return Content("Done|" + application.Id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadApplicationLogo(IFormFile File, int EntityId)
    {
        var application = await _applicationServices.GetById(EntityId);

        CodeGenerator codeGenerator = new();
        application.ApplicationKey = codeGenerator.GenerateAppKey(application.Id);

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Application/Logos/");
        var baseName = Path.GetFileNameWithoutExtension(application.LogoFileName);

        var uploadResult = await _fileUploadService.SaveImageAsync(File, savePath, baseName);
        if (!uploadResult.Succeeded)
            return Content("Failed");

        application.LogoFileName = uploadResult.FileName;
        await _applicationServices.Update(application);

        return Content("Done");
    }

    [Route("/BackOffice/Application/SelectAppToEnter/{applicationId}")]
    public async Task<IActionResult> SelectAppToEnter(int applicationId)
    {
        if (!await CheckUserApproval())
            return Redirect("/WaitingForApproval");

        // HttpContext.Session.SetInt32("AppKey", applicationId);
        // CookieOptions option = new();
        // option.Expires = DateTime.Now.AddDays(1);
        // Response.Cookies.Append("AppKey", applicationId.ToString(), option);
        // 
        await _userManagementServices.SetCurrentApplicationId(User.Identity.Name, applicationId);

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
