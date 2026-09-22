namespace Web.Areas.BackOffice.Controllers;
[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class AccessManagementController : BaseController
{
    //fields
    #region fields
    private readonly IApplicationServices _applicationServices;
    private readonly ISectorServices _sectorServices;
    private readonly ISectorEntityServices _SectorEntityServices;
    private readonly IEntityAccessServices _entityAccessServices;
    private readonly ICurrentApplicationContext _currentApplicationContext;
    private readonly ISender _sender;
    #endregion

    // constructor
    #region constructor
    public AccessManagementController(IApplicationServices applicationServices,
        ISectorServices sectorServices, ISectorEntityServices SectorEntityServices,
        IEntityAccessServices entityAccessServices,
        ICurrentApplicationContext currentApplicationContext, ISender sender)
    {
        _sectorServices = sectorServices;
        _applicationServices = applicationServices;
        _SectorEntityServices = SectorEntityServices;
        _entityAccessServices = entityAccessServices;
        _currentApplicationContext = currentApplicationContext;
        _sender = sender;
    }
    #endregion

    // methods
    #region sectors
    [HttpGet]
    public IActionResult Sectors()
    {
        return View();
    }

    [HttpGet]
    public IActionResult SectorForm()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SectorList()
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        return View(await _sectorServices.GetAllSector(currentApplicationId));
    }

    [HttpPost]
    public async Task<IActionResult> SaveSector(SectorDto sector)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        sector.ApplicationId = currentApplicationId;
        if (sector.Id == 0)
            await _sectorServices.Create(sector);
        else
            await _sectorServices.Update(sector, currentApplicationId);
        return Content("Done");
    }
    #endregion

    #region sector entity
    [HttpGet("/{area}/{controller}/EntityForm/{sectorId}")]
    public async Task<IActionResult> EntityForm(int sectorId)
    {
        ViewData["SectorId"] = sectorId;
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var sector = await _sectorServices.GetById(sectorId, currentApplicationId);
        ViewData["SectorTitle"] = sector.Title;
        var entity = new SectorEntityDto
        {
            SectorId = sectorId
        };
        return View(entity);
    }

    [HttpGet("/{area}/{controller}/EntityList/{sectorId}")]
    public async Task<IActionResult> EntityList(int sectorId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        return View(await _SectorEntityServices.GetSectorEntities(sectorId, currentApplicationId));
    }

    [HttpPost]
    public async Task<IActionResult> SaveEntity(SectorEntityDto entity)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        if (entity.Id == 0)
            await _SectorEntityServices.Create(entity, currentApplicationId);
        else
            await _SectorEntityServices.Update(entity, currentApplicationId);

        return Content("Done");
    }
    #endregion

    #region access
    [HttpGet]
    public IActionResult Accesses()
    {
        return View();
    }
    [HttpGet]
    public async Task<IActionResult> AccessForm(int id = 0)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var sectors = await _sectorServices.GetAllSector(currentApplicationId);
        ViewData["Sectors"] = sectors;
        ViewData["SectorEntities"] = await _SectorEntityServices.GetSectorEntities(sectors[0].Id, currentApplicationId);

        if (id != 0)
        {
            var access = await _entityAccessServices.GetById(id, currentApplicationId);
            var entity = await _SectorEntityServices.GetById(access.EntityId, currentApplicationId);
            ViewData["SectorId"] = entity.SectorId;
            ViewData["EntityId"] = access.EntityId;
            return View(access);
        }
        else
            return View();
    }

    [HttpGet("/{area}/{controller}/GetSectorEntities/{id}")]
    public async Task<IActionResult> GetSectorEntities(int id)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var sectorEntities = await _SectorEntityServices.GetSectorEntities(id, currentApplicationId);
        return PartialView("_SectorEntityOptionsPartial", sectorEntities);
    }
    [HttpGet]
    public async Task<IActionResult> AccessList()
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        ViewData["SectorEntities"] = await _SectorEntityServices.GetEntitiesForApplication(currentApplicationId);
        return View(await _entityAccessServices.List(currentApplicationId));
    }

    [HttpPost]
    public async Task<IActionResult> SaveAccess(EntityAccessDto accessModel)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        if (accessModel.Id == 0)
        {
            await _entityAccessServices.Create(accessModel, currentApplicationId);
        }
        else
            await _entityAccessServices.Update(accessModel, currentApplicationId);

        return Content("Done");
    }
    #endregion
}
