using Application.UseCases.TranslatorServices;

namespace Web.Areas.BackOffice.Controllers;

// Legacy Farsi bulk translation queue. The whole controller is SuperAdmin-only through the named
// policy, so every action here (candidates, bulk submit, progress, recovered jobs) inherits it - a Content access key
// such as Content.ChangeActivity is not enough. The application comes solely from the selected
// BackOffice application and the culture solely from ContentTranslation options, never from the
// request.
[Authorize(Policy = WebAuthorizationPolicies.SuperAdmin)]
[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class LegacyFarsiTranslationQueueController : BaseController
{
    private readonly LegacyFarsiTranslationCandidates _candidates;
    private readonly LegacyFarsiTranslationBulkQueue _bulkQueue;
    private readonly LegacyFarsiTranslationProgress _progress;
    private readonly LegacyFarsiTranslationRecoveredJobs _recoveredJobs;
    private readonly ContentTranslationOptions _options;
    private readonly ICurrentApplicationContext _currentApplicationContext;

    public LegacyFarsiTranslationQueueController(LegacyFarsiTranslationCandidates candidates, LegacyFarsiTranslationBulkQueue bulkQueue,
        LegacyFarsiTranslationProgress progress, LegacyFarsiTranslationRecoveredJobs recoveredJobs, ContentTranslationOptions options,
        ICurrentApplicationContext currentApplicationContext)
    {
        _candidates = candidates;
        _bulkQueue = bulkQueue;
        _progress = progress;
        _recoveredJobs = recoveredJobs;
        _options = options;
        _currentApplicationContext = currentApplicationContext;
    }

    // Only content IDs are accepted; any other JSON property is ignored.
    public record QueueRequest(int[] ContentIds);

    // The only configuration the dashboard sees: the selectable type IDs, the per-request limit,
    // whether background translation is switched on (configuration, not worker health) and how long
    // completed jobs stay in the recovered list.
    public record DashboardViewModel(IReadOnlyList<int> TypeIds, int MaxItems, bool WorkerEnabled, int RecentJobDays);

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Legacy Farsi Translation Queue";
        return View(new DashboardViewModel(_options.LegacyBulkCandidateTypeIds, _options.BulkRequestMaxItems, _options.WorkerEnabled,
            _options.LegacyBulkRecentJobDays));
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

    // Queues background translation of the selected content (antiforgery via the global filter and
    // the X-CSRF-TOKEN header). An empty, over-limit or non-positive request is rejected whole.
    // Never translates inline.
    [HttpPost]
    public async Task<IActionResult> Queue([FromBody] QueueRequest request, CancellationToken cancellationToken)
    {
        var items = ModelState.IsValid && request?.ContentIds != null
            ? await _bulkQueue.Queue(request.ContentIds, _currentApplicationContext.RequireApplicationId(), cancellationToken)
            : null;
        if (items == null)
            return BadRequest(new { error = $"Select 1 to {_options.BulkRequestMaxItems} content items with positive IDs." });

        return Json(new { items = items.Select(i => new { contentId = i.ContentId, outcome = i.Outcome.ToString(), jobId = i.JobId }) });
    }

    // Read-only state of the job IDs a Queue response returned (?jobIds=12&jobIds=13). An empty,
    // malformed, over-limit or non-positive request is rejected whole. A job outside the selected
    // application or configured culture is NotFound. failureReason is a fixed message for Failed jobs only,
    // never the stored error code. Never queues, retries or translates.
    [HttpGet]
    public async Task<IActionResult> Progress([FromQuery] int[] jobIds, CancellationToken cancellationToken)
    {
        var items = ModelState.IsValid && jobIds != null
            ? await _progress.Read(jobIds, _currentApplicationContext.RequireApplicationId(), cancellationToken)
            : null;
        if (items == null)
            return BadRequest(new { error = $"Request 1 to {_options.BulkRequestMaxItems} positive job IDs." });

        return Json(new
        {
            items = items.Select(i => new
            {
                jobId = i.JobId, contentId = i.ContentId, state = i.State.ToString(), attemptCount = i.AttemptCount, failureReason = i.FailureReason
            })
        });
    }

    // Read-only: the selected application's active and recently completed jobs for the configured
    // culture, so the dashboard can show them again after a refresh, with per-state counts over every page.
    // failureReason is as for Progress.
    // Paging is clamped as for Candidates.
    // Never queues, retries, fingerprints or translates.
    [HttpGet]
    public async Task<IActionResult> RecoveredJobs(int page = 1, int pageSize = LegacyFarsiTranslationCandidates.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var result = await _recoveredJobs.Find(page, pageSize, _currentApplicationContext.RequireApplicationId(), cancellationToken);
        return Json(new
        {
            cultureAvailable = result.CultureAvailable,
            items = result.Items.Select(i => new
            {
                jobId = i.JobId, contentId = i.ContentId, title = i.Title, typeId = i.TypeId, isActive = i.IsActive, state = i.State.ToString(),
                attemptCount = i.AttemptCount, failureReason = i.FailureReason, relevantAt = i.RelevantAt
            }),
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            counts = new
            {
                queued = result.Counts.Queued, processing = result.Counts.Processing, succeeded = result.Counts.Succeeded, failed = result.Counts.Failed,
                superseded = result.Counts.Superseded, active = result.Counts.Active, total = result.Counts.Total
            }
        });
    }
}
