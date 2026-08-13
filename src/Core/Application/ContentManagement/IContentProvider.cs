using Application.ResultModels;
using Domains.Entities.ContentManagement;

namespace Application.ContentManagement;

public interface IContentProvider
{
    // Internal/legacy escape hatch: returns the full tracked Content entity graph (images,
    // metadata, sections, elements) rather than a DTO. Kept deliberately, for the admin Farsi
    // translation/activation flow in Web/Areas/BackOffice/Controllers/ContentController.cs
    // (FarsiContentForm, SaveFarsiContentForm, ChangeContentActiveMode) and
    // Web/Controllers/HomeController.cs, which mutate the entity in place and save it back. Do
    // not add new callers of this outside that flow — prefer DTO-returning reads for anything
    // else.
    Task<Content> GetContentForTranslate(int contentId);

    ContentListResultModel GetContentsListByCategoryId(int applicationId, int categoryId, int pageIndex = 0, int pageSize = 20, string keyLang = "en");
    ContentListResultModel GetContentsListByTagId(int applicationId, int tagId, int pageIndex = 0, int pageSize = 20);
}
