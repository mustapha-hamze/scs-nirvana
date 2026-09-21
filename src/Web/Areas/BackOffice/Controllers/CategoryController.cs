namespace Web.Areas.BackOffice.Controllers;

[Authorize]

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
    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Form()
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        ViewData["Categories"] = await _categoryServices.List(currentApplicationId);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveForm(CategoryDto category)
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        await _categoryServices.Create(category, currentApplicationId);
        //TODO: Implement Realistic Implementation
        return Content("Done");
    }

    public async Task<IActionResult> List()
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        var categories = await _categoryServices.List(currentApplicationId);

        return View(categories);
    }
    #endregion
}