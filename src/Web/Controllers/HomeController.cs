namespace Web.Controllers;

public class HomeController : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // Reached two ways: directly, and as the production exception handler's re-execution target
    // (Program.cs) - which can carry the original request's verb and body, so this must accept
    // any HTTP method. It never reads exception state, so nothing sensitive is at risk either way.
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Error()
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
