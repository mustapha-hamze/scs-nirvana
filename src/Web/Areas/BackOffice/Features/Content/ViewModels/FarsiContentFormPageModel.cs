using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;

namespace Web.Areas.BackOffice.Features.Content.ViewModels;

// Inherits FarsiContentEditDto (rather than wrapping it) so FarsiContentForm.cshtml's asp-for
// bindings keep their unprefixed field names. The inherited fields hold the editable translated
// text; everything else is read-only master data for display.
public sealed class FarsiContentFormPageModel : FarsiContentEditDto
{
    public int TypeId { get; init; }
    public ManualTranslationSeed Seed { get; init; }
    public TranslationStatus? TranslationStatus { get; init; }

    // Current English master: source text, layout, element types/titles/sizes and media.
    public Domains.Entities.ContentManagement.Content Source { get; init; }
    public IReadOnlyDictionary<int, SectionElement> SourceElements { get; init; }
}
