namespace Web.Areas.Api;
[ApiController]
public class CategoryController : ControllerBase
{
    private readonly ISender _sender;
    public CategoryController(ISender sender)
    {
        _sender = sender;
    }
    [HttpGet]
    [Route("api/[controller]/GetCategories/{applicationId}/{parent?}")]
    public async Task<ActionResult> GetCategories(int applicationId, int parent = 0)
    {
        var categories = await _sender.Send(new GetCategoriesQuery(applicationId, parent));

        return Ok(categories);
    }
}
