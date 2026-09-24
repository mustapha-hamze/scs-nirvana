namespace Web.Areas.Api;
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ContentController : ControllerBase
{
    private readonly IContentServices _contentServices;
    public ContentController(IContentServices contentServices)
    {
        _contentServices = contentServices;
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
}
