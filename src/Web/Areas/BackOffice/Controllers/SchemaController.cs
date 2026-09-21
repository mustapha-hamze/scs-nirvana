namespace Web.Areas.BackOffice.Controllers;

[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class SchemaController : BaseController
{
    // fields
    #region fields
    private readonly ISchemaServices _schemaServices;
    private readonly IHostEnvironment _appEnvironment;
    private readonly ISystemTypeServices _systemTypeServices;
    private readonly IUserManagementServices _userManagementServices;
    private readonly IFileUploadService _fileUploadService;
    #endregion

    // constructor
    #region constructor
    public SchemaController(ISchemaServices schemaServices, IHostEnvironment appEnvironment,
        ISystemTypeServices systemTypeServices, IUserManagementServices userManagementServices,
        IFileUploadService fileUploadService)
    {
        _appEnvironment = appEnvironment;
        _schemaServices = schemaServices;
        _systemTypeServices = systemTypeServices;
        _userManagementServices = userManagementServices;
        _fileUploadService = fileUploadService;
    }
    #endregion

    // methods
    #region methods
    public IActionResult Index()
    {
        return View();
    }

    [Route("/{area}/Schema/SchemaForm/{id?}")]
    public async Task<IActionResult> SchemaForm(int id = 0)
    {
        if (id == 0)
        {
            return View(new SchemaDto
            {
                LogoFileName = Guid.NewGuid().ToString()
            });
        }
        else
        {
            var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
            return View(await _schemaServices.GetById(id, user.CurrentApplicationId));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSchemaForm(SchemaDto schema)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        if (schema.Id == 0)
        {
            schema = await _schemaServices.Create(schema, user.CurrentApplicationId);
            return Content("Done|" + schema.Id.ToString());
        }
        else
        {
            schema = await _schemaServices.Update(schema, user.CurrentApplicationId);
            return Content("Done|" + schema.Id.ToString());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadSchemaLogo(IFormFile File, int EntityId)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var schema = await _schemaServices.GetById(EntityId, user.CurrentApplicationId);

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Schema/Logos/");
        var baseName = Path.GetFileNameWithoutExtension(schema.LogoFileName);

        var uploadResult = await _fileUploadService.SaveImageAsync(File, savePath, baseName);
        if (!uploadResult.Succeeded)
            return Content("Failed");

        schema.LogoFileName = uploadResult.FileName;
        await _schemaServices.Update(schema, user.CurrentApplicationId);

        return Content("Done");
    }

    public async Task<IActionResult> SchemaList()
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(await _schemaServices.List(user.CurrentApplicationId));
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Route("/{area}/Schema/DeleteSchema/{id}")]
    public async Task<IActionResult> DeleteSchema(int id)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        await _schemaServices.Delete(id, user.CurrentApplicationId);
        return Content("Done");
    }


    [Route("/{area}/Schema/SchemaDetailsForm/{schemaId}")]
    public async Task<IActionResult> SchemaDetailsForm(int schemaId)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["SchemaId"] = schemaId;
        ViewData["Types"] = await _systemTypeServices.GetTypesInTypeGroup(user.CurrentApplicationId, TypeId.ContentSchema);
        return View();
    }

    [Route("/{area}/Schema/SchemaDetailsList/{schemaId}")]
    public async Task<IActionResult> SchemaDetailsList(int schemaId)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(await _schemaServices.DetailsList(schemaId, user.CurrentApplicationId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SchemaDetailsFormSave(SchemaDetailsDto schemaDetails)
    {
        //TODO: Implement Realistic Implementation
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        await _schemaServices.CreateDetails(schemaDetails, user.CurrentApplicationId);
        return Content("Done");
    }
    #endregion

}
