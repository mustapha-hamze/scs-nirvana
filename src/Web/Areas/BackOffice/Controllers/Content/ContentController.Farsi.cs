using Application.UseCases.TranslatorServices;
using Web.Areas.BackOffice.Features.Content;

namespace Web.Areas.BackOffice.Controllers;

// Farsi localization: manual editing of the activation culture's canonical ContentTranslation
// text against the current English master. ManualContentTranslation owns seeding and validation;
// FarsiContentMapper only maps the text-only form contract. The legacy FarsiContent is never
// written here.
public partial class ContentController
{
    [HttpGet]
    [RequireAccess(AccessKeys.Content.EditFarsi)]
    [Route("/{area}/{controller}/FarsiContentForm/{id}/{typeId}")]
    public async Task<IActionResult> FarsiContentForm(int id, int typeId)
    {
        var editor = await _manualTranslation.GetEditor(id, _translationOptions.ActivationCultureId, _currentApplicationContext.RequireApplicationId());
        if (editor == null)
            return NotFound();

        return View(FarsiContentMapper.ToPageModel(editor, typeId));
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Content.EditFarsi)]
    public async Task<IActionResult> SaveFarsiContentForm(FarsiContentEditDto model)
    {
        if (model == null || model.Id == 0)
            return Content("Failed");
        // DataAnnotations limits (incl. nested metadata/section-element DTOs) before any persistence;
        // field names only, never the submitted text.
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                translationState = "InvalidInput",
                fields = ModelState.Where(e => e.Value.Errors.Count > 0).Select(e => e.Key).OrderBy(k => k, StringComparer.Ordinal)
            });

        var result = await _manualTranslation.Save(model.Id, _translationOptions.ActivationCultureId, _currentApplicationContext.RequireApplicationId(),
            model.SourceFingerprint, FarsiContentMapper.ToLocalizedText(model));
        return result switch
        {
            ManualTranslationSaveResult.Saved => Content("Done"),
            ManualTranslationSaveResult.NotFound => NotFound(),
            _ => Conflict(new { translationState = result.ToString() })
        };
    }
}
