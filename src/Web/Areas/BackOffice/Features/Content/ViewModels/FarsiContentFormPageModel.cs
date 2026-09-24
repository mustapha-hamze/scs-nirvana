namespace Web.Areas.BackOffice.Features.Content.ViewModels;

// Inherits FarsiContentEditDto (rather than wrapping it) so FarsiContentForm.cshtml's asp-for
// bindings keep their unprefixed field names - SaveFarsiContentForm's FarsiContentEditDto-bound
// model binder is unchanged.
public sealed class FarsiContentFormPageModel : FarsiContentEditDto
{
    public bool FarsiInitializedFromEnglish { get; init; }
}
