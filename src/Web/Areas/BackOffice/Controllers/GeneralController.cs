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
    private readonly ICurrentApplicationContext _currentApplicationContext;
    #endregion

    // constructor
    #region constructor
    public GeneralController(ITagServices tagServices, ICultureServices cultureServices, IApplicationServices applicationServices,
        ISystemTypeServices systemTypeServices, ICurrentApplicationContext currentApplicationContext)
    {
        _applicationServices = applicationServices;
        _tagServices = tagServices;
        _cultureServices = cultureServices;
        _systemTypeServices = systemTypeServices;
        _currentApplicationContext = currentApplicationContext;
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
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        await _tagServices.Create(tag, currentApplicationId);
        return Content(tag.TypeId.ToString());
    }

    public async Task<IActionResult> TagList()
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        return View(await _tagServices.List(currentApplicationId));
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
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        await _applicationServices.CreateApplicationSetting(applicationSetting, currentApplicationId);
        return Content("Done");
    }

    public async Task<IActionResult> ApplicationSettingList()
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        return View(await _applicationServices.GetApplicationSetting(currentApplicationId));
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
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        systemType.ApplicationId = currentApplicationId;
        systemType.IsActive = true;
        await _systemTypeServices.Create(systemType);
        // return View();
        return Content("Done");
    }
    public async Task<IActionResult> SystemTypesList()
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        return View(await _systemTypeServices.List(currentApplicationId));
    }
    #endregion
}
