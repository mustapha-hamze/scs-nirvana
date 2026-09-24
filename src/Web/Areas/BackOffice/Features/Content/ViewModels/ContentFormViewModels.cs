using Web.Areas.BackOffice.Presentation.Shell;

namespace Web.Areas.BackOffice.Features.Content.ViewModels;

// Web Phase 4 task 3: strongly typed replacements for the Content feature's ViewData/ViewBag/cast
// usage (Index, ContentForm + its three partials, ContentList + its per-row action partial).

public sealed record ContentIndexViewModel(int TypeId, string TypeTitle, bool CanCreateContent);

// Inherits ContentDto (rather than wrapping it) so ContentForm.cshtml's existing asp-for="Title"
// style bindings keep emitting the same unprefixed field names SaveContentForm's
// ContentDto-bound model binder already expects - wrapping would prefix every field name
// ("Content.Title") and silently break that POST contract.
public sealed class ContentFormViewModel : ContentDto
{
    public bool IsNew => Id == 0;

    // Id/Title pairs only - exactly what the Type <select> needs, sourced from the same
    // BackOfficeShellContext snapshot the sidebar already resolves for this request instead of a
    // second GetTypesInTypeGroup call.
    public IReadOnlyList<BackOfficeContentTypeLink> Types { get; init; } = Array.Empty<BackOfficeContentTypeLink>();

    // The {typeId} route value, kept distinct from the inherited TypeId (which reflects the
    // content's own persisted type once loaded) so a mismatched URL can never change this page's
    // sidebar-highlighting behavior from what ViewData["TypeId"] used to show.
    public int RouteTypeId { get; init; }

    public string WebsiteUrl { get; init; }
    public string ContentPreviewUrl => IsNew ? null : $"{WebsiteUrl}/{Id}";

    // Save (create) vs. Update (edit) - already resolved to the correct dynamic key server-side.
    public bool CanSaveOrUpdateContent { get; init; }
    public bool CanChangeActivity { get; init; }

    // ContentTranslation:ActivationCultureId, rendered for the request-translation control;
    // 0 = not configured, so the control shows as unavailable.
    public int ActivationCultureId { get; init; }
    public bool CanPreviewBody { get; init; }
    public bool CanPreviewImages { get; init; }
    public bool CanPreviewAttachments { get; init; }
    public bool CanPreviewRelations { get; init; }
    public bool CanPreviewMetadata { get; init; }
}

public sealed class ContentListItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; }
    public int TypeId { get; init; }
    public string TypeTitle { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDT { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
}

// Replaces the old comma-concatenated ViewData["PartialData"] ("id,typeId") string contract, and
// resolves CanEdit/CanDelete once for the whole list instead of once per row.
public sealed class ContentListViewModel
{
    public IReadOnlyList<ContentListItemViewModel> Items { get; init; } = Array.Empty<ContentListItemViewModel>();
}
