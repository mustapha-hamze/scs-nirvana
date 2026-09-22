using Web.Areas.BackOffice.Features.Content;

namespace Web.Areas.BackOffice.Controllers;

// Farsi localization: editing/saving the FarsiContent JSON blob stored alongside the English
// Content row. Pure JSON parsing/mapping lives in FarsiContentMapper; this partial only handles
// HTTP concerns, tenant scoping, and delegating to IContentProvider/IContentServices.
public partial class ContentController
{
    [HttpGet]
    [RequireAccess(AccessKeys.Content.EditFarsi)]
    [Route("/{area}/{controller}/FarsiContentForm/{id}/{typeId}")]
    public async Task<IActionResult> FarsiContentForm(int id, int typeId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var englishContent = await _contentProvider.GetContentForTranslate(id, currentApplicationId);
        if (englishContent == null)
            return NotFound();

        ViewData["TypeId"] = typeId;

        var (source, usedEnglishFallback) = FarsiContentMapper.GetEditSource(englishContent);
        ViewBag.FarsiInitializedFromEnglish = usedEnglishFallback;

        return View(FarsiContentMapper.ToEditDto(source));
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Content.EditFarsi)]
    public async Task<IActionResult> SaveFarsiContentForm(FarsiContentEditDto model)
    {
        if (model == null || model.Id == 0)
            return Content("Failed");

        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var englishContent = await _contentProvider.GetContentForTranslate(model.Id, currentApplicationId);
        if (englishContent == null)
            return NotFound();

        var (baseContent, _) = FarsiContentMapper.GetEditSource(englishContent);

        FarsiContentMapper.ApplyEdit(baseContent, model);

        var farsiJson = FarsiContentMapper.SerializeForStorage(baseContent);

        // UpdateTranslate now queries without AsNoTracking, so it safely resolves to the
        // already-tracked `englishContent` instance instead of conflicting with it.
        await _contentServices.UpdateTranslate(model.Id, farsiJson, currentApplicationId);

        return Content("Done");
    }
}
