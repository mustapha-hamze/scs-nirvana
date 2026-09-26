using Application.UseCases.TranslatorServices;
using Web.Areas.BackOffice.Features.Content;
using Web.Areas.BackOffice.Features.Content.ViewModels;

namespace Web.Areas.BackOffice.Controllers;

// Farsi localization: editing/saving the FarsiContent JSON blob stored alongside the English
// Content row. Pure JSON parsing/mapping lives in FarsiContentMapper; this partial only handles
// HTTP concerns, tenant scoping, and delegating to IContentProvider/IContentServices.
public partial class ContentController
{
    private async Task<(Domains.Entities.ContentManagement.Content Source, bool UsedEnglishFallback)> GetFarsiEditSource(
        Domains.Entities.ContentManagement.Content englishContent, int applicationId)
    {
        var canonicalText = await _manualTranslation.GetStoredText(englishContent.Id, _translationOptions.ActivationCultureId, applicationId);
        return FarsiContentMapper.GetEditSource(englishContent, canonicalText);
    }

    [HttpGet]
    [RequireAccess(AccessKeys.Content.EditFarsi)]
    [Route("/{area}/{controller}/FarsiContentForm/{id}/{typeId}")]
    public async Task<IActionResult> FarsiContentForm(int id, int typeId)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        // Fingerprint before the form's source read: a source edit in between makes the save
        // conflict rather than mark stale text Ready.
        var sourceFingerprint = await _manualTranslation.GetSourceFingerprint(id, currentApplicationId);
        var englishContent = await _contentProvider.GetContentForTranslate(id, currentApplicationId);
        if (sourceFingerprint == null || englishContent == null)
            return NotFound();

        var (source, usedEnglishFallback) = await GetFarsiEditSource(englishContent, currentApplicationId);
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
            SourceFingerprint = sourceFingerprint,
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

        // Same rebased graph the form was rendered from, so its IDs line up with the current master.
        var (baseContent, _) = await GetFarsiEditSource(englishContent, currentApplicationId);

        FarsiContentMapper.ApplyEdit(baseContent, model);

        var farsiJson = FarsiContentMapper.SerializeForStorage(baseContent);

        // Validated against the current master, then FarsiContent and the activation culture's
        // Ready ContentTranslation commit together; any failure leaves both unchanged.
        var result = await _manualTranslation.Save(model.Id, _translationOptions.ActivationCultureId, currentApplicationId,
            model.SourceFingerprint, baseContent, farsiJson);
        return result switch
        {
            ManualTranslationSaveResult.Saved => Content("Done"),
            ManualTranslationSaveResult.NotFound => NotFound(),
            _ => Conflict(new { translationState = result.ToString() })
        };
    }
}
