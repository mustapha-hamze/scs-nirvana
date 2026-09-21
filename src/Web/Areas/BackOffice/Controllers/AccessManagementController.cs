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
    private readonly IUserManagementServices _userManagementServices;
    private readonly ISender _sender;
    #endregion

    // constructor 
    #region constructor
    public AccessManagementController(IApplicationServices applicationServices,
        ISectorServices sectorServices, ISectorEntityServices SectorEntityServices,
        IEntityAccessServices entityAccessServices,
        IUserManagementServices userManagementServices, ISender sender)
    {
        _sectorServices = sectorServices;
        _applicationServices = applicationServices;
        _SectorEntityServices = SectorEntityServices;
        _entityAccessServices = entityAccessServices;
        _userManagementServices = userManagementServices;
        _sender = sender;
    }
    #endregion

    // methods
    #region sectors
    public IActionResult Sectors()
    {
        return View();
    }

    public IActionResult SectorForm()
    {
        return View();
    }

    public async Task<IActionResult> SectorList()
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(await _sectorServices.GetAllSector(user.CurrentApplicationId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSector(SectorDto sector)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        sector.ApplicationId = user.CurrentApplicationId;
        if (sector.Id == 0)
            await _sectorServices.Create(sector);
        else
            await _sectorServices.Update(sector, user.CurrentApplicationId);
        return Content("Done");
    }
    #endregion

    #region sector entity
    [HttpGet("/{area}/{controller}/EntityForm/{sectorId}")]
    public async Task<IActionResult> EntityForm(int sectorId)
    {
        ViewData["SectorId"] = sectorId;
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var sector = await _sectorServices.GetById(sectorId, user.CurrentApplicationId);
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
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(await _SectorEntityServices.GetSectorEntities(sectorId, user.CurrentApplicationId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEntity(SectorEntityDto entity)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        if (entity.Id == 0)
            await _SectorEntityServices.Create(entity, user.CurrentApplicationId);
        else
            await _SectorEntityServices.Update(entity, user.CurrentApplicationId);

        return Content("Done");
    }
    #endregion

    #region access
    public IActionResult Accesses()
    {
        return View();
    }
    public async Task<IActionResult> AccessForm(int id = 0)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var sectors = await _sectorServices.GetAllSector(user.CurrentApplicationId);
        ViewData["Sectors"] = sectors;
        ViewData["SectorEntities"] = await _SectorEntityServices.GetSectorEntities(sectors[0].Id, user.CurrentApplicationId);

        if (id != 0)
        {
            var access = await _entityAccessServices.GetById(id, user.CurrentApplicationId);
            var entity = await _SectorEntityServices.GetById(access.EntityId, user.CurrentApplicationId);
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
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var sectorEntities = await _SectorEntityServices.GetSectorEntities(id, user.CurrentApplicationId);
        return PartialView("_SectorEntityOptionsPartial", sectorEntities);
    }
    public async Task<IActionResult> AccessList()
    {
        ViewData["SectorEntities"] = await _SectorEntityServices.GetAllEntities();
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(await _entityAccessServices.List(user.CurrentApplicationId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAccess(EntityAccessDto accessModel)
    {
        var user = await _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        if (accessModel.Id == 0)
        {
            await _entityAccessServices.Create(accessModel, user.CurrentApplicationId);
        }
        else
            await _entityAccessServices.Update(accessModel, user.CurrentApplicationId);

        return Content("Done");
    }
    #endregion
}
