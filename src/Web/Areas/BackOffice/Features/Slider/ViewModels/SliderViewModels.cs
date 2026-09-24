namespace Web.Areas.BackOffice.Features.Slider.ViewModels;

// Web Phase 4 task 4: strongly typed replacements for the Slider feature's ViewData/view-injected
// service usage (List, Create, item form/list, and the (unreferenced) create-slider nav button).

public sealed class SliderListItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; }
    public bool CanAccessItems { get; init; }
}

public sealed class SliderListViewModel
{
    public IReadOnlyList<SliderListItemViewModel> Items { get; init; } = Array.Empty<SliderListItemViewModel>();
}

public sealed record SliderCreateViewModel(int ApplicationId, bool CanSave);

// Views/Slider/_CreateSliderButton.cshtml is never actually rendered by any page (no
// Html.PartialAsync call references it) - kept in the strongly typed style for consistency, not
// because it's reachable.
public sealed record SliderCreateButtonViewModel(bool CanCreateSlider);

// Inherits SliderItem (rather than wrapping it) so GetSliderItemForm.cshtml's asp-for bindings
// keep their unprefixed field names - CreateItem/UpdateItem's SliderItem-bound model binders are
// unchanged.
public sealed class SliderItemFormViewModel : Domains.Entities.CustomModule.SliderItem
{
    // One cache-busting suffix per rendered response, applied to every image URL on the page -
    // same as the view's previous per-render `_version` local.
    public Guid ImageVersion { get; init; }
    public bool CanCreateItem { get; init; }
    public bool CanUpdateItem { get; init; }
}

public sealed class SliderItemRowViewModel
{
    public int Id { get; init; }
    public int SliderId { get; init; }
    public string Title { get; init; }
    public string ImageFileName { get; init; }
    public bool IsActive { get; init; }
}

public sealed class SliderItemListViewModel
{
    public IReadOnlyList<SliderItemRowViewModel> Items { get; init; } = Array.Empty<SliderItemRowViewModel>();
    public Guid ImageVersion { get; init; }
    public bool CanToggleActivity { get; init; }
    public bool CanDelete { get; init; }
    public bool CanUpdate { get; init; }
}
