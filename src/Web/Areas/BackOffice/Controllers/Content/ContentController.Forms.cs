using Application.UseCases.TranslatorServices;
using Web.Areas.BackOffice.Features.Content.ViewModels;

namespace Web.Areas.BackOffice.Controllers;

// Content forms/lifecycle: the entry list, the create/edit form, saving it, and active-mode
// changes (activation requires a Ready translation; translation itself is queued separately and
// runs in the background).
public partial class ContentController
{
    [HttpGet]
    [RequireAccess(AccessKeys.Content.Module)]
    [Route("/{area}/{controller}/Index/{id}")]
    public async Task<IActionResult> Index(int id)
    {
        var shell = await _shellContext.GetSnapshotAsync();
        var typeTitle = shell.ContentTypes.FirstOrDefault(t => t.Id == id)?.Title
            ?? shell.AppPages.FirstOrDefault(p => p.PageType == id.ToString())?.Title
            ?? "Content";
        var canCreateContent = shell.Access.CanAccess(AccessKeys.Content.Add);

        ViewData["Title"] = typeTitle;
        return View(new ContentIndexViewModel(id, typeTitle, canCreateContent));
    }

    // Shared by two UI entry points with different keys (_CreateContentButton.cshtml's Add vs.
    // _ContentListActionsButton.cshtml's Edit) - the required key depends on id, so it's checked
    // in the body via DenyIfMissingAccessAsync rather than a static [RequireAccess].
    [HttpGet]
    [Route("/{area}/{controller}/ContentForm/{id}/{typeId}")]
    public async Task<IActionResult> ContentForm(int id = 0, int typeId = 0)
    {
        if (await DenyIfMissingAccessAsync(id == 0 ? AccessKeys.Content.Add : AccessKeys.Content.Edit) is IActionResult deny)
            return deny;

        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var shell = await _shellContext.GetSnapshotAsync();

        ContentDto content;
        string websiteUrl = null;
        if (id != 0)
        {
            content = await _contentServices.GetById(id, currentApplicationId);
            var appSetting = await _applicationServices.GetApplicationSetting(currentApplicationId, 5000);
            websiteUrl = appSetting[0].Value;
        }
        else
        {
            content = new ContentDto
            {
                TypeId = typeId,
                PublishDt = DateTime.Now // Set default publish date to now
            };
        }

        var canSaveOrUpdateContent = shell.Access.CanAccess(id == 0 ? AccessKeys.Content.Save : AccessKeys.Content.Update);
        var canChangeActivity = shell.Access.CanAccess(AccessKeys.Content.ChangeActivity);
        var canPreviewBody = shell.Access.CanAccess(AccessKeys.Content.PreviewBody);
        var canPreviewImages = shell.Access.CanAccess(AccessKeys.Content.PreviewImages);
        var canPreviewAttachments = shell.Access.CanAccess(AccessKeys.Content.PreviewAttachments);
        var canPreviewRelations = shell.Access.CanAccess(AccessKeys.Content.PreviewRelations);
        var canPreviewMetadata = shell.Access.CanAccess(AccessKeys.Content.PreviewMetadata);

        var model = new ContentFormViewModel
        {
            Id = content.Id,
            ApplicationId = content.ApplicationId,
            TypeId = content.TypeId,
            Title = content.Title,
            HeadLine = content.HeadLine,
            Abstract = content.Abstract,
            Description = content.Description,
            Categories = content.Categories,
            Tags = content.Tags,
            Cultures = content.Cultures,
            PublishDt = content.PublishDt,
            Status = content.Status,
            IsDeleted = content.IsDeleted,
            IsActive = content.IsActive,
            UpdatedDT = content.UpdatedDT,
            CreatedDT = content.CreatedDT,
            Types = shell.ContentTypes,
            RouteTypeId = typeId,
            WebsiteUrl = websiteUrl,
            CanSaveOrUpdateContent = canSaveOrUpdateContent,
            CanChangeActivity = canChangeActivity,
            ActivationCultureId = _translationOptions.ActivationCultureId,
            CanPreviewBody = canPreviewBody,
            CanPreviewImages = canPreviewImages,
            CanPreviewAttachments = canPreviewAttachments,
            CanPreviewRelations = canPreviewRelations,
            CanPreviewMetadata = canPreviewMetadata,
        };
        return View(model);
    }

    // Same shared-action reasoning as ContentForm above (SAVE_1003 for a new content vs.
    // UPDATE_1001 for an existing one - see _ContentFormSaveButton.cshtml).
    [HttpPost]
    public async Task<IActionResult> SaveContentForm(ContentDto content)
    {
        if (await DenyIfMissingAccessAsync(content.Id == 0 ? AccessKeys.Content.Save : AccessKeys.Content.Update) is IActionResult deny)
            return deny;

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

    [HttpGet]
    [RequireAccess(AccessKeys.Content.Module)]
    [Route("/{area}/{controller}/{action}/{id}")]
    public async Task<IActionResult> ContentList(int id)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        var contents = (await _contentServices.List(currentApplicationId)).Where(c => c.TypeId == id).ToList();
        var shell = await _shellContext.GetSnapshotAsync();
        var canEdit = shell.Access.CanAccess(AccessKeys.Content.Edit);
        var canDelete = shell.Access.CanAccess(AccessKeys.Content.Delete);

        var items = contents.Select(c => new ContentListItemViewModel
        {
            Id = c.Id,
            Title = c.Title,
            TypeId = c.TypeId,
            TypeTitle = shell.ContentTypes.FirstOrDefault(t => t.Id == c.TypeId)?.Title,
            IsActive = c.IsActive,
            CreatedDT = c.CreatedDT,
            CanEdit = canEdit,
            CanDelete = canDelete,
        }).ToList();

        return View(new ContentListViewModel { Items = items });
    }

    // Queues (or returns the existing) background translation of the content's current source
    // into cultureId. Never calls the provider inline.
    [HttpPost]
    [RequireAccess(AccessKeys.Content.ChangeActivity)]
    [Route("/{area}/Content/RequestTranslation/{contentId}/{cultureId}")]
    public async Task<IActionResult> RequestTranslation(int contentId, int cultureId)
    {
        var result = await _translationRequests.Request(contentId, cultureId, _currentApplicationContext.RequireApplicationId());
        if (result == null)
            return NotFound();
        if (result.State == ContentTranslationState.CultureUnavailable)
            return Conflict(new { translationState = result.State.ToString() });

        return Json(new { translationState = result.State.ToString(), jobId = result.JobId });
    }

    [HttpPost]
    [RequireAccess(AccessKeys.Content.ChangeActivity)]
    [Route("/{area}/Content/ChangeContentActiveMode/{typeId}/{contentId}/{mode}")]
    public async Task<IActionResult> ChangeContentActiveMode(int typeId, int contentId, bool mode)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();

        if (mode)
        {
            // Activation only checks for a Ready translation of the current source; it never
            // translates, waits, or treats legacy FarsiContent as ready. Otherwise 409 with the
            // state, and the content stays inactive.
            if (_translationOptions.ActivationCultureId == 0)
                return Conflict(new { translationState = "CultureNotConfigured" });

            var state = await _translationRequests.GetState(contentId, _translationOptions.ActivationCultureId, currentApplicationId);
            if (state == null)
                return NotFound();
            if (state != ContentTranslationState.Ready)
                return Conflict(new { translationState = state.ToString() });
        }

        await _contentServices.ChangeContentActiveMode(contentId, mode, currentApplicationId);

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
    [RequireAccess(AccessKeys.Content.Delete)]
    [Route("/{area}/Content/DeleteContent/{id}")]
    public async Task<IActionResult> DeleteContent(int id)
    {
        var currentApplicationId = _currentApplicationContext.RequireApplicationId();
        await _contentServices.Delete(id, currentApplicationId);
        return Content("Done");
    }
}
