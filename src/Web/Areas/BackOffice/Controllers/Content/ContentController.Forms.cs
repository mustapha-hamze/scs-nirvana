namespace Web.Areas.BackOffice.Controllers;

// Content forms/lifecycle: the entry list, the create/edit form, saving it, and active-mode
// changes (which trigger Farsi auto-translate on first activation).
public partial class ContentController
{
    [Route("/{area}/{controller}/Index/{id}")]
    public IActionResult Index(int id)
    {
        ViewData["TypeId"] = id;
        return View();
    }

    [Route("/{area}/{controller}/ContentForm/{id}/{typeId}")]
    public async Task<IActionResult> ContentForm(int id = 0, int typeId = 0)
    {
        ViewData["TypeId"] = typeId;
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();

        ViewData["Types"] = await _systemTypeServices.GetTypesInTypeGroup(currentApplicationId, TypeId.Content);

        if (id != 0)
        {
            var content = await _contentServices.GetById(id, currentApplicationId);
            var appSetting = await _applicationServices.GetApplicationSetting(currentApplicationId, 5000);
            ViewData["WebsiteUrl"] = appSetting[0].Value;
            return View(content);
        }
        else
        {
            return View(new ContentDto
            {
                TypeId = typeId,
                PublishDt = DateTime.Now // Set default publish date to now
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveContentForm(ContentDto content)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        if (content.Id == 0)
        {
            content.ApplicationId = currentApplicationId;
            var _content = await _contentServices.Create(content);
            return RedirectToAction("ContentForm", new { id = _content.Id, typeId = _content.TypeId });
        }
        else
        {
            var _content = await _contentServices.GetById(content.Id, currentApplicationId);
            content.Categories = _content.Categories;
            content.Tags = _content.Tags;
            content.Cultures = _content.Cultures;
            await _contentServices.Update(content, currentApplicationId);

            return RedirectToAction("ContentForm", new { id = content.Id, typeId = _content.TypeId });
        }
    }

    [Route("/{area}/{controller}/{action}/{id}")]
    public async Task<IActionResult> ContentList(int id)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var contents = (await _contentServices.List(currentApplicationId)).Where(c => c.TypeId == id).ToList();
        ViewData["Types"] = await _systemTypeServices.GetTypesInTypeGroup(currentApplicationId, TypeId.Content);
        return View(contents);
    }

    [HttpPost]
    [Route("/{area}/Content/ChangeContentActiveMode/{typeId}/{contentId}/{mode}")]
    public async Task<IActionResult> ChangeContentActiveMode(int typeId, int contentId, bool mode)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();

        if (mode)
        {
            var content = await _contentProvider.GetContentForTranslate(contentId, currentApplicationId);
            if (content == null)
                return NotFound();

            if (string.IsNullOrEmpty(content.FarsiContent))
            {
                var result = await _contentTranslator.Translate(content);
                await _contentServices.ActivateTranslatedContent(contentId, result, currentApplicationId);
            }
        }
        else
        {
            await _contentServices.ChangeContentActiveMode(contentId, mode, currentApplicationId);
        }

        var frontContentTypes = await _applicationServices.GetApplicationSetting(currentApplicationId, 1002);
        if (frontContentTypes.Any(x => x.Value.Contains(typeId.ToString())))
        {
            var frontContentTypeIds = new List<int>();
            foreach (var contentType in frontContentTypes)
                frontContentTypeIds.Add(Convert.ToInt32(contentType.Value));
        }

        return Content("Done");
    }

    [HttpDelete]
    [Route("/{area}/Content/DeleteContent/{id}")]
    public async Task<IActionResult> DeleteContent(int id)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        await _contentServices.Delete(id, currentApplicationId);
        return Content("Done");
    }
}
