namespace Web.Areas.BackOffice.Controllers;

[Authorize]

[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class GeneralController : BaseController
{
    // fields
    #region fields
    private readonly ITagServices _tagServices;
    private readonly ICultureServices _cultureServices;
    private readonly IApplicationServices _applicationServices;
    private readonly ISystemTypeServices _systemTypeServices;
    private readonly IUserManagementServices _userManagementServices;
    #endregion

    // constructor
    #region constructor
    public GeneralController(ITagServices tagServices, ICultureServices cultureServices, IApplicationServices applicationServices,
        ISystemTypeServices systemTypeServices, IUserManagementServices userManagementServices)
    {
        _applicationServices = applicationServices;
        _tagServices = tagServices;
        _cultureServices = cultureServices;
        _systemTypeServices = systemTypeServices;
        _userManagementServices = userManagementServices;
    }
    #endregion

    // methods
    #region Tags
    public IActionResult Tags()
    {
        return View();
    }

    public IActionResult TagForm()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTagForm(TagDto tag)
    {
        //TODO: Implement Realistic Implementation
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        await _tagServices.Create(tag, user.CurrentApplicationId);
        return Content(tag.TypeId.ToString());
    }

    public async Task<IActionResult> TagList()
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(await _tagServices.List(user.CurrentApplicationId));
    }
    #endregion

    #region  Cultures
    public IActionResult Cultures()
    {
        return View();
    }
    public IActionResult CultureForm()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCultureForm(CultureDto culture)
    {
        await _cultureServices.Create(culture);
        //TODO: Implement Realistic Implementation
        return Content("Done");
    }

    public async Task<IActionResult> CultureList()
    {
        return View(await _cultureServices.List());
    }
    #endregion

    #region System Logs
    public IActionResult Logs()
    {
        return View();
    }
    #endregion

    #region Application Setting
    public IActionResult ApplicationSetting()
    {
        return View();
    }

    public IActionResult ApplicationSettingForm()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplicationSettingForm(ApplicationSettingDto applicationSetting)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        await _applicationServices.CreateApplicationSetting(applicationSetting, user.CurrentApplicationId);
        return Content("Done");
    }

    public async Task<IActionResult> ApplicationSettingList()
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(await _applicationServices.GetApplicationSetting(user.CurrentApplicationId));
    }
    #endregion

    #region  System Types
    public IActionResult SystemTypes()
    {
        return View();
    }
    public IActionResult SystemTypeForm()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SaveSystemTypeForm(SystemTypeDto systemType)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        systemType.ApplicationId = user.CurrentApplicationId;
        systemType.IsActive = true;
        await _systemTypeServices.Create(systemType);
        // return View();
        return Content("Done");
    }
    public async Task<IActionResult> SystemTypesList()
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(await _systemTypeServices.List(user.CurrentApplicationId));
    }
    #endregion
}
