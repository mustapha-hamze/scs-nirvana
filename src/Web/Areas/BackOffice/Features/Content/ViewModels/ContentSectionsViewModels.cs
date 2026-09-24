namespace Web.Areas.BackOffice.Features.Content.ViewModels;

public sealed class ContentSectionsViewModel
{
    public IReadOnlyList<SchemaDto> Schemas { get; init; } = Array.Empty<SchemaDto>();
    public IReadOnlyList<SectionDto> Sections { get; init; } = Array.Empty<SectionDto>();
    public int Priority { get; init; }
    public bool CanSaveBody { get; init; }
}

public sealed class CreateContentSectionViewModel
{
    public IReadOnlyList<SchemaDetailsDto> SchemaDetails { get; init; } = Array.Empty<SchemaDetailsDto>();
    public int Priority { get; init; }
}
