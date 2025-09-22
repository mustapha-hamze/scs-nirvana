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
        //TODO: Implement Realistic Implementation
        return View();
    }

    public IActionResult Form()
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["Categories"] = _categoryServices.List(user.CurrentApplicationId);
        //TODO: Implement Realistic Implementation
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveForm(CategoryDto category)
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        category.ApplicationId = user.CurrentApplicationId;
        await _categoryServices.Create(category);
        //TODO: Implement Realistic Implementation
        return Content("Done");
    }

    public IActionResult List()
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        //TODO: Implement Realistic Implementation
        return View(_categoryServices.List(user.CurrentApplicationId));
    }
    #endregion
}