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

    public IActionResult SectorList()
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(_sectorServices.GetAllSector(user.CurrentApplicationId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSector(SectorDto sector)
    {
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        sector.ApplicationId = user.CurrentApplicationId;
        if (sector.Id == 0)
            await _sectorServices.Create(sector);
        else
            await _sectorServices.Update(sector);
        return Content("Done");
    }
    #endregion

    #region sector entity
    [HttpGet("/{area}/{controller}/EntityForm/{sectorId}")]
    public async Task<IActionResult> EntityForm(int sectorId)
    {
        ViewData["SectorId"] = sectorId;
        var sector = await _sectorServices.GetById(sectorId);
        ViewData["SectorTitle"] = sector.Title;
        var entity = new SectorEntityDto
        {
            SectorId = sectorId
        };
        return View(entity);
    }

    [HttpGet("/{area}/{controller}/EntityList/{sectorId}")]
    public IActionResult EntityList(int sectorId)
    {
        return View(_SectorEntityServices.GetSectorEntities(sectorId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEntity(SectorEntityDto entity)
    {
        if (entity.Id == 0)
            await _SectorEntityServices.Create(entity);
        else
            await _SectorEntityServices.Update(entity);

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
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        var sectors = _sectorServices.GetAllSector(user.CurrentApplicationId);
        ViewData["Sectors"] = sectors;
        ViewData["SectorEntities"] = _SectorEntityServices.GetSectorEntities(sectors[0].Id);

        if (id != 0)
        {
            var access = await _entityAccessServices.GetById(id);
            var entity = await _SectorEntityServices.GetById(access.EntityId);
            ViewData["SectorId"] = entity.SectorId;
            ViewData["EntityId"] = access.EntityId;
            return View(access);
        }
        else
            return View();
    }

    [HttpGet("/{area}/{controller}/GetSectorEntities/{id}")]
    public string GetSectorEntities(int id)
    {
        var sectorEntities = _SectorEntityServices.GetSectorEntities(id);

        string html = string.Empty;
        foreach (var item in sectorEntities)
        {
            html += "<option value='" + item.Id + "'>" + item.Title + "</option>";
        }

        return html;
    }
    public IActionResult AccessList()
    {
        ViewData["SectorEntities"] = _SectorEntityServices.GetAllEntities();
        var user = _userManagementServices.GetUserByEmailAddress(User.Identity.Name);
        return View(_entityAccessServices.List(user.CurrentApplicationId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAccess(EntityAccessDto accessModel)
    {
        if (accessModel.Id == 0)
        {
            await _entityAccessServices.Create(accessModel);
        }
        else
            await _entityAccessServices.Update(accessModel);

        return Content("Done");
    }
    #endregion
}
