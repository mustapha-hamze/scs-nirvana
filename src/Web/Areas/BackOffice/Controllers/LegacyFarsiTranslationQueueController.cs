using Application.UseCases.TranslatorServices;

namespace Web.Areas.BackOffice.Controllers;

// Legacy Farsi bulk translation queue. The whole controller is SuperAdmin-only through the named
// policy, so every later action here (bulk submit, progress) inherits it - a Content access key
// such as Content.ChangeActivity is not enough. The application comes solely from the selected
// BackOffice application and the culture solely from ContentTranslation options, never from the
// request.
[Authorize(Policy = WebAuthorizationPolicies.SuperAdmin)]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class LegacyFarsiTranslationQueueController : BaseController
{
    private readonly LegacyFarsiTranslationCandidates _candidates;
    private readonly ICurrentApplicationContext _currentApplicationContext;

    public LegacyFarsiTranslationQueueController(LegacyFarsiTranslationCandidates candidates,
        ICurrentApplicationContext currentApplicationContext)
    {
        _candidates = candidates;
        _currentApplicationContext = currentApplicationContext;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Legacy Farsi Translation Queue";
        return View();
    }

    // Read-only: never queues, translates or writes. Undefined Sort values fail binding.
    [HttpGet]
    public async Task<IActionResult> Candidates([FromQuery] LegacyFarsiTranslationCandidateQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var page = await _candidates.Find(query, _currentApplicationContext.RequireApplicationId(), cancellationToken);
        return Json(page);
    }
}
