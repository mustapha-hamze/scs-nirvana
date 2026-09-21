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
    private readonly ICurrentApplicationContext _currentApplicationContext;
    private readonly IFileUploadService _fileUploadService;
    #endregion

    // constructor
    #region constructor
    public SchemaController(ISchemaServices schemaServices, IHostEnvironment appEnvironment,
        ISystemTypeServices systemTypeServices, ICurrentApplicationContext currentApplicationContext,
        IFileUploadService fileUploadService)
    {
        _appEnvironment = appEnvironment;
        _schemaServices = schemaServices;
        _systemTypeServices = systemTypeServices;
        _currentApplicationContext = currentApplicationContext;
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
            var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
            return View(await _schemaServices.GetById(id, currentApplicationId));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSchemaForm(SchemaDto schema)
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        if (schema.Id == 0)
        {
            schema = await _schemaServices.Create(schema, currentApplicationId);
            return Content("Done|" + schema.Id.ToString());
        }
        else
        {
            schema = await _schemaServices.Update(schema, currentApplicationId);
            return Content("Done|" + schema.Id.ToString());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadSchemaLogo(IFormFile File, int EntityId)
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        var schema = await _schemaServices.GetById(EntityId, currentApplicationId);

        var savePath = Path.Combine(_appEnvironment.ContentRootPath, "wwwroot/Storage/Schema/Logos/");
        var baseName = Path.GetFileNameWithoutExtension(schema.LogoFileName);

        var uploadResult = await _fileUploadService.SaveImageAsync(File, savePath, baseName);
        if (!uploadResult.Succeeded)
            return Content("Failed");

        schema.LogoFileName = uploadResult.FileName;
        await _schemaServices.Update(schema, currentApplicationId);

        return Content("Done");
    }

    public async Task<IActionResult> SchemaList()
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        return View(await _schemaServices.List(currentApplicationId));
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Route("/{area}/Schema/DeleteSchema/{id}")]
    public async Task<IActionResult> DeleteSchema(int id)
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        await _schemaServices.Delete(id, currentApplicationId);
        return Content("Done");
    }


    [Route("/{area}/Schema/SchemaDetailsForm/{schemaId}")]
    public async Task<IActionResult> SchemaDetailsForm(int schemaId)
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        ViewData["SchemaId"] = schemaId;
        ViewData["Types"] = await _systemTypeServices.GetTypesInTypeGroup(currentApplicationId, TypeId.ContentSchema);
        return View();
    }

    [Route("/{area}/Schema/SchemaDetailsList/{schemaId}")]
    public async Task<IActionResult> SchemaDetailsList(int schemaId)
    {
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        return View(await _schemaServices.DetailsList(schemaId, currentApplicationId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SchemaDetailsFormSave(SchemaDetailsDto schemaDetails)
    {
        //TODO: Implement Realistic Implementation
        var currentApplicationId = _currentApplicationContext.CurrentApplicationId ?? 0;
        await _schemaServices.CreateDetails(schemaDetails, currentApplicationId);
        return Content("Done");
    }
    #endregion

}
