using Application.UseCases.TranslatorServices;

namespace Web.Areas.Api;
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ContentController : ControllerBase
{
    private readonly IContentServices _contentServices;
    private readonly LocalizedContentReader _localizedContentReader;
    private readonly ILogger<ContentController> _logger;
    public ContentController(IContentServices contentServices, LocalizedContentReader localizedContentReader, ILogger<ContentController> logger)
    {
        _contentServices = contentServices;
        _localizedContentReader = localizedContentReader;
        _logger = logger;
    }

    [HttpGet]
    [Route("api/[controller]/GetContent/{applicationId}/{id}")]
    public async Task<ActionResult> Get(int applicationId, int id, CancellationToken cancellationToken)
    {
        var content = await _contentServices.GetContentByIdFull(id, applicationId, cancellationToken);
        if (content.Count > 0)
            return Ok(content[0]);

        return Ok(content);
    }

    // These two routes used to overlap ({typeId} vs {typeId}/{pageIndex?}), so a request to
    // .../GetContentByTypeId/{typeId} with no page segment matched both actions and could throw
    // AmbiguousMatchException. Making the second route's {pageIndex} required (not optional)
    // keeps both existing URL shapes working while making each match exactly one action.
    [HttpGet]
    [Route("api/[controller]/GetContentByTypeId/{applicationId}/{typeId}")]
    public async Task<ActionResult> GetContentByTypeId(int applicationId, int typeId, CancellationToken cancellationToken)
    {
        return Ok(await _contentServices.GetContentByTypeId(typeId, applicationId, cancellationToken));
    }

    [HttpGet]
    [Route("api/[controller]/GetContentByTypeId/{applicationId}/{typeId}/{pageIndex}")]
    public async Task<ActionResult> GetContentByTypeId(int applicationId, int typeId, int pageIndex, CancellationToken cancellationToken)
    {
        return Ok(await _contentServices.GetContentByTypeId(typeId, applicationId, pageIndex, cancellationToken));
    }

    [HttpGet]
    [Route("api/[controller]/GetContentByCategoryId/{applicationId}/{categoryId}/{pageIndex?}/{pageSize?}")]
    public async Task<ActionResult> GetContentByCategoryId(int applicationId, int categoryId, int pageIndex = 0, int pageSize = 40, CancellationToken cancellationToken = default)
    {
        return Ok(await _contentServices.GetContentByCategoryId(categoryId, applicationId, pageIndex, pageSize, cancellationToken));
    }

    [HttpGet]
    [Route("api/[controller]/GetContentByCategoryIdByDate/{applicationId}/{categoryId}/{startDate}/{endDate}/{pageIndex?}")]
    public async Task<ActionResult> GetContentByCategoryIdByDate(int applicationId, int categoryId,
        DateTime startDate, DateTime endDate, int pageIndex = 0, CancellationToken cancellationToken = default)
    {
        return Ok(
            await _contentServices
                .GetContentByCategoryIdByDate(categoryId, applicationId, startDate, endDate, pageIndex, cancellationToken));
    }

    [HttpGet]
    [Route("api/[controller]/GetContentInCategoryAsBox/{applicationId}/{categoryId}")]
    public async Task<ActionResult> GetContentInCategoryAsBox(int applicationId, int categoryId, CancellationToken cancellationToken)
    {
        return Ok(
            await _contentServices
                .GetContentInCategoryAsBox(categoryId, applicationId, cancellationToken));
    }

    // Culture-aware read (docs/Translation-Modernization-Plan.md, Phase 5). Returns the current
    // content with its text resolved for ?culture= (a Ready translation of the current source,
    // else legacy Farsi for the configured legacy culture, else English) plus Culture/Resolution
    // metadata. 400 for a malformed culture; 404 when the content isn't the application's or the
    // ContentTranslation:LocalizedRead* rollout gates are closed for it. Existing routes are untouched.
    [HttpGet]
    [Route("api/[controller]/GetLocalizedContent/{applicationId}/{id}")]
    public async Task<ActionResult> GetLocalizedContent(int applicationId, int id, [FromQuery] string culture, CancellationToken cancellationToken)
    {
        var result = await _localizedContentReader.Read(id, applicationId, culture, cancellationToken);
        switch (result.Status)
        {
            case LocalizedContentReadStatus.Found:
                // Privacy-safe: never content text or the raw request value.
                _logger.LogInformation("Localized content read for application {ApplicationId}, culture {Culture}: {Resolution}",
                    applicationId, result.Content.Culture, result.Content.Resolution);
                return Ok(result.Content);
            case LocalizedContentReadStatus.InvalidCulture:
                ModelState.AddModelError(nameof(culture), "culture must be a language tag such as fa-IR.");
                return ValidationProblem();
            default:
                return NotFound();
        }
    }
}
