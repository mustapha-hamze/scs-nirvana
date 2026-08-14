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
            return View(await _schemaServices.GetById(id));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSchemaForm(SchemaDto schema)
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        if (schema.Id == 0)
        {
            schema.ApplicationId = user.CurrentApplicationId;
            schema = await _schemaServices.Create(schema);
            return Content("Done|" + schema.Id.ToString());
        }
        else
        {
            schema.ApplicationId = user.CurrentApplicationId;
            schema = await _schemaServices.Update(schema);
            return Content("Done|" + schema.Id.ToString());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadSchemaLogo(IFormFile File, int EntityId)
    {
        var schema = await _schemaServices.GetById(EntityId);

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Schema/Logos/");
        var baseName = Path.GetFileNameWithoutExtension(schema.LogoFileName);

        var uploadResult = await _fileUploadService.SaveImageAsync(File, savePath, baseName);
        if (!uploadResult.Succeeded)
            return Content("Failed");

        schema.LogoFileName = uploadResult.FileName;
        await _schemaServices.Update(schema);

        return Content("Done");
    }

    public IActionResult SchemaList()
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(_schemaServices.List(user.CurrentApplicationId));
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Route("/{area}/Schema/DeleteSchema/{id}")]
    public async Task<IActionResult> DeleteSchema(int id)
    {
        await _schemaServices.Delete(id);
        return Content("Done");
    }


    [Route("/{area}/Schema/SchemaDetailsForm/{schemaId}")]
    public IActionResult SchemaDetailsForm(int schemaId)
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        ViewData["SchemaId"] = schemaId;
        ViewData["Types"] = _systemTypeServices.GetTypesInTypeGroup(user.CurrentApplicationId, TypeId.ContentSchema);
        return View();
    }

    [Route("/{area}/Schema/SchemaDetailsList/{schemaId}")]
    public IActionResult SchemaDetailsList(int schemaId)
    {
        return View(_schemaServices.DetailsList(schemaId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SchemaDetailsFormSave(SchemaDetailsDto schemaDetails)
    {
        //TODO: Implement Realistic Implementation
        await _schemaServices.CreateDetails(schemaDetails);
        return Content("Done");
    }
    #endregion

}