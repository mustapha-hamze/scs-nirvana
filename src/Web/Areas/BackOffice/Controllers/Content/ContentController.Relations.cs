namespace Web.Areas.BackOffice.Controllers;

// Relations (categories/tags/cultures) and metadata (SEO title/author/keywords/description).
public partial class ContentController
{
    [HttpGet]
    [Route("/{area}/Content/ContentRelations/{contentId}")]
    public async Task<IActionResult> ContentRelations(int contentId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        ViewData["Categories"] = await _categoryServices.GetAllFullPath(currentApplicationId);
        ViewData["Tags"] = await _tagServices.FindTagsByTypeId(currentApplicationId, TypeId.Content);
        ViewData["Cultures"] = await _cultureServices.List();
        var content = await _contentServices.GetById(contentId, currentApplicationId);
        return View(content);
    }

    [HttpGet]
    [Route("/{area}/Content/ContentMetadata/{contentId}")]
    public async Task<IActionResult> ContentMetadata(int contentId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var contentMetadata = await _contentServices.GetContentMetadata(contentId, currentApplicationId);
        contentMetadata.ContentId = contentId;

        return View(contentMetadata);
    }

    [HttpPost]
    [Route("/{area}/Content/SaveRelation/{Entity}/{contentId}")]
    public async Task<IActionResult> SaveRelation([FromForm] string Data, string Entity, int contentId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var ids = ParseRelationIds(Data);
        switch (Entity)
        {
            case "Category":
                await _contentServices.CreateContentCategories(ids, contentId, currentApplicationId);
                break;
            case "Tag":
                await _contentServices.CreateContentTags(ids, contentId, currentApplicationId);
                break;
            case "Culture":
                await _contentServices.CreateContentCultures(ids, contentId, currentApplicationId);
                break;
        }

        return Content("Done");
    }

    // The client always appends a trailing '|' to the pipe-delimited id list; strips it, then
    // parses each remaining token as an id (same as the previous Convert.ToInt32 per-token
    // behavior — malformed input still throws rather than being silently dropped).
    private static List<int> ParseRelationIds(string data)
    {
        if (string.IsNullOrEmpty(data))
            return new List<int>();

        var trimmed = data[..^1];
        if (trimmed.Length == 0)
            return new List<int>();

        return trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();
    }

    [HttpPost]
    public async Task<IActionResult> SaveContentMetadata(ContentMetadataDto contentMetadata)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();

        if (contentMetadata.Id == 0)
            await _contentServices.CreateContentMetadata(contentMetadata, currentApplicationId);
        else
            await _contentServices.UpdateContentMetadata(contentMetadata, currentApplicationId);

        return Content("Done");
    }
}
