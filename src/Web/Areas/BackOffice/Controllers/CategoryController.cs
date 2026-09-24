namespace Web.Areas.BackOffice.Controllers;

// The sidebar only exposes one access key for Category (Views/Shared/_SideBarCMS.cshtml:
// "CMS1000_1002"), with no finer-grained per-action key - every action here shares it.
[Authorize]
[RequireAccess(AccessKeys.Category.Module)]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class CategoryController : BaseController
{
    // fields
    #region fields
    private readonly ICategoryServices _categoryServices;
    private readonly ICurrentApplicationContext _currentApplicationContext;
    #endregion

    // constructor
    #region constructor
    public CategoryController(ICategoryServices categoryServices,
        ICurrentApplicationContext currentApplicationContext)
    {
        _categoryServices = categoryServices;
        _currentApplicationContext = currentApplicationContext;
    }
    #endregion


    // methods
    #region methods
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Form()
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        ViewData["Categories"] = await _categoryServices.List(currentApplicationId);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SaveForm(CategoryDto category)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        await _categoryServices.Create(category, currentApplicationId);
        //TODO: Implement Realistic Implementation
        return Content("Done");
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var categories = await _categoryServices.List(currentApplicationId);

        return View(categories);
    }
    #endregion
}