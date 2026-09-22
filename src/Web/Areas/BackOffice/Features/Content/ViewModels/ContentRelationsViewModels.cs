namespace Web.Areas.BackOffice.Features.Content.ViewModels;

public sealed class ContentRelationsViewModel
{
    public int ContentId { get; init; }
    public string CategoriesRelated { get; init; }
    public string TagsRelated { get; init; }
    public string CulturesRelated { get; init; }
    public IReadOnlyList<CategoryDto> Categories { get; init; } = Array.Empty<CategoryDto>();
    public IReadOnlyList<TagDto> Tags { get; init; } = Array.Empty<TagDto>();
    public IReadOnlyList<CultureDto> Cultures { get; init; } = Array.Empty<CultureDto>();
    public bool CanSaveRelations { get; init; }
}

// Inherits ContentMetadataDto so the form's asp-for bindings keep their unprefixed field names -
// SaveContentMetadata's ContentMetadataDto-bound model binder is unchanged.
public sealed class ContentMetadataPageModel : ContentMetadataDto
{
    public bool CanSaveMetadata { get; init; }
}
