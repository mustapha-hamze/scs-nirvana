using Web.Areas.BackOffice.Features.Content;
using Web.Areas.BackOffice.Features.Content.ViewModels;

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

        var (source, usedEnglishFallback) = FarsiContentMapper.GetEditSource(englishContent);
        var dto = FarsiContentMapper.ToEditDto(source);

        var model = new FarsiContentFormPageModel
        {
            Id = dto.Id,
            ApplicationId = dto.ApplicationId,
            TypeId = typeId, // the route value - preserves the prior ViewData["TypeId"] behavior exactly
            Title = dto.Title,
            HeadLine = dto.HeadLine,
            Abstract = dto.Abstract,
            Description = dto.Description,
            PublishDt = dto.PublishDt,
            Metadata = dto.Metadata,
            Sections = dto.Sections,
            FarsiInitializedFromEnglish = usedEnglishFallback,
        };
        return View(model);
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
