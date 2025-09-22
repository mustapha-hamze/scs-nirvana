namespace Web.Areas.BackOffice.Controllers;
[Authorize]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class HomeController : BaseController
{
    // private readonly ICacheRepository _cacheRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;
    public HomeController(/*ICacheRepository cacheRepository,*/ UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext, IMapper mapper)
    {
        // _cacheRepository = cacheRepository;
        _userManager = userManager;
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public IActionResult Index()
    {
        return View();
    }

    // public async Task<IActionResult> RefereshCacheDb(string entity)
    // {
    //     var user = await _userManager.FindByNameAsync(User.Identity.Name);

    //     switch (entity)
    //     {
    //         case "Event":
    //             _cacheRepository.RefreshEventsCacheDb(user.CurrentApplicationId);
    //             break;
    //         case "Tag":
    //             _cacheRepository.RefreshTagsCacheDb(user.CurrentApplicationId);
    //             break;
    //     }

    //     return Ok();
    // }

    // public IActionResult AddTicketsToCacheDB(string password)
    // {
    //     if (password == "Mustapha9876")
    //     {
    //         var tickets = _mapper.Map<List<EventTicketDto>>(_dbContext.Tickets.Where(t => t.Status == 100).ToList());
    //         _cacheRepository.AddTicketsToCacheDb(tickets);
    //         return Ok("Done");
    //     }
    //     return BadRequest("Access invalid.");
    // }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
