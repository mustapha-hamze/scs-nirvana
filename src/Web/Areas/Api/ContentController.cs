namespace Web.Areas.Api;
[ApiController]
public class ContentController : ControllerBase
{
    private readonly IContentServices _contentServices;
    public ContentController(IContentServices contentServices)
    {
        _contentServices = contentServices;
    }

    [HttpGet]
    [Route("api/[controller]/GetContent/{id}")]
    public ActionResult Get(int id)
    {
        var content = _contentServices.GetContentByIdFull(id);
        if (content.Count > 0)
            return Ok(content[0]);

        return Ok(content);
    }

    // These two routes used to overlap ({typeId} vs {typeId}/{pageIndex?}), so a request to
    // .../GetContentByTypeId/{typeId} with no page segment matched both actions and could throw
    // AmbiguousMatchException. Making the second route's {pageIndex} required (not optional)
    // keeps both existing URL shapes working while making each match exactly one action.
    [HttpGet]
    [Route("api/[controller]/GetContentByTypeId/{typeId}")]
    public ActionResult GetContentByTypeId(int typeId)
    {
        return Ok(_contentServices.GetContentByTypeId(typeId));
    }

    [HttpGet]
    [Route("api/[controller]/GetContentByTypeId/{typeId}/{pageIndex}")]
    public ActionResult GetContentByTypeId(int typeId, int pageIndex)
    {
        return Ok(_contentServices.GetContentByTypeId(typeId, pageIndex));
    }

    [HttpGet]
    [Route("api/[controller]/GetContentByCategoryId/{categoryId}/{pageIndex?}/{pageSize?}")]
    public ActionResult GetContentByCategoryId(int categoryId, int pageIndex = 0, int pageSize = 40)
    {
        return Ok(_contentServices.GetContentByCategoryId(categoryId, pageIndex, pageSize));
    }

    [HttpGet]
    [Route("api/[controller]/GetContentByCategoryIdByDate/{categoryId}/{startDate}/{endDate}/{pageIndex?}")]
    public ActionResult GetContentByCategoryIdByDate(int categoryId,
        DateTime startDate, DateTime endDate, int pageIndex = 0)
    {
        return Ok(
            _contentServices
                .GetContentByCategoryIdByDate(categoryId, startDate, endDate, pageIndex));
    }

    [HttpGet]
    [Route("api/[controller]/GetContentInCategoryAsBox/{categoryId}")]
    public ActionResult GetContentInCategoryAsBox(int categoryId)
    {
        return Ok(
            _contentServices
                .GetContentInCategoryAsBox(categoryId));
    }
}