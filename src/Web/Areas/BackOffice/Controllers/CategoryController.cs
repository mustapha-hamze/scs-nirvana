namespace Web.Areas.BackOffice.Controllers;

[Authorize]

[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class CategoryController : BaseController
{
    // fields
    #region fields
    private readonly ICategoryServices _categoryServices;
    private readonly IUserManagementServices _userManagementServices;
    #endregion

    // constructor
    #region constructor
    public CategoryController(ICategoryServices categoryServices,
        IUserManagementServices userManagementServices)
    {
        _categoryServices = categoryServices;
        _userManagementServices = userManagementServices;
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
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["Categories"] = await _categoryServices.List(user.CurrentApplicationId);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveForm(CategoryDto category)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        await _categoryServices.Create(category, user.CurrentApplicationId);
        //TODO: Implement Realistic Implementation
        return Content("Done");
    }

    public async Task<IActionResult> List()
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var categories = await _categoryServices.List(user.CurrentApplicationId);

        return View(categories);
    }
    #endregion
}