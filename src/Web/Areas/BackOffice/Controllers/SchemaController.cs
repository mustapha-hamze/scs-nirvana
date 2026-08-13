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
    #endregion

    // constructor
    #region constructor            
    public SchemaController(ISchemaServices schemaServices, IHostEnvironment appEnvironment,
        ISystemTypeServices systemTypeServices, IUserManagementServices userManagementServices)
    {
        _appEnvironment = appEnvironment;
        _schemaServices = schemaServices;
        _systemTypeServices = systemTypeServices;
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

    [Route("/{area}/Schema/SchemaForm/{id?}")]
    public async Task<IActionResult> SchemaForm(int id = 0)
    {
        if (id == 0)
        {
            //TODO: Implement Realistic Implementation
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
    public async Task<IActionResult> UploadSchemaLogo(IFormFile File, int EntityId, string Extension)
    {
        var schema = await _schemaServices.GetById(EntityId);

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Schema/Logos/");

        var _extension = Extension.Split('/');

        schema.LogoFileName = schema.LogoFileName + "." + _extension[1];

        await _schemaServices.Update(schema);

        if (await UploadImage(File, savePath, schema.LogoFileName))
            return Content("Done");
        else
            return Content("Failed");
    }

    public IActionResult SchemaList()
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        //TODO: Implement Realistic Implementation
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
        //TODO: Implement Realistic Implementation
        return View();
    }

    [Route("/{area}/Schema/SchemaDetailsList/{schemaId}")]
    public IActionResult SchemaDetailsList(int schemaId)
    {
        //TODO: Implement Realistic Implementation
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