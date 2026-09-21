using Application.ResultModels;
using Domains.Entities.ContentManagement;

namespace Application.ContentManagement;

public interface IContentProvider
{
    // Internal/legacy escape hatch: returns the full tracked Content entity graph (images,
    // metadata, sections, elements) rather than a DTO, for the admin Farsi translation/
    // activation flow in Web/Areas/BackOffice/Controllers/ContentController.cs
    // (FarsiContentForm, SaveFarsiContentForm, ChangeContentActiveMode), which mutates the
    // entity in place and saves it back. Do not add new callers of this outside that flow —
    // prefer DTO-returning reads for anything else. applicationId is required: a cross-
    // application contentId must be rejected rather than silently translating/activating
    // another application's content. There is deliberately no bare-contentId overload.
    Task<Content> GetContentForTranslate(int contentId, int applicationId, CancellationToken cancellationToken = default);

    Task<ContentListResultModel> GetContentsListByCategoryId(int applicationId, int categoryId, int pageIndex = 0, int pageSize = 20, string keyLang = "en", CancellationToken cancellationToken = default);
    Task<ContentListResultModel> GetContentsListByTagId(int applicationId, int tagId, int pageIndex = 0, int pageSize = 20, CancellationToken cancellationToken = default);
}
