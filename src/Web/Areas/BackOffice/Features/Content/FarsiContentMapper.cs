using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Web.Areas.BackOffice.Features.Content.ViewModels;

namespace Web.Areas.BackOffice.Features.Content;

// Pure mapping between the Application's manual translation model and the Farsi form's text-only
// contract. No data access, no Content graph is built or bound.
internal static class FarsiContentMapper
{
    public static FarsiContentFormPageModel ToPageModel(ManualTranslationEditor editor, int typeId)
    {
        var text = editor.Text;
        var source = editor.Source;
        return new FarsiContentFormPageModel
        {
            Id = source.Id,
            TypeId = typeId, // the route value - preserves the prior ViewData["TypeId"] behavior exactly
            SourceFingerprint = editor.SourceFingerprint,
            Seed = editor.Seed,
            TranslationStatus = editor.TranslationStatus,
            Source = source,
            SourceElements = (source.Sections ?? new List<ContentSection>()).SelectMany(s => s.Elements ?? new List<SectionElement>()).ToDictionary(e => e.Id),
            Title = text.Title,
            HeadLine = text.HeadLine,
            Abstract = text.Abstract,
            Description = text.Description,
            Metadata = text.Metadata == null ? null : new FarsiContentMetadataEditDto
            {
                Id = text.Metadata.Id,
                Title = text.Metadata.Title,
                Author = text.Metadata.Author,
                Keywords = text.Metadata.Keywords,
                Description = text.Metadata.Description
            },
            Sections = text.Sections.Select(s => new FarsiSectionEditDto
            {
                Id = s.Id,
                SectionElements = s.Elements.Select(e => new FarsiSectionElementEditDto { Id = e.Id, TinyText = e.TinyText, EditorText = e.EditorText }).ToList()
            }).ToList()
        };
    }

    // Posted form -> the use case's input; structure is validated there, not here. Null
    // sections/elements lists (nothing posted) mean empty.
    public static LocalizedContentText ToLocalizedText(FarsiContentEditDto model) => new(
        model.Title, model.HeadLine, model.Abstract, model.Description,
        model.Metadata == null ? null
            : new LocalizedMetadataText(model.Metadata.Id, model.Metadata.Title, model.Metadata.Author, model.Metadata.Keywords, model.Metadata.Description),
        (model.Sections ?? new List<FarsiSectionEditDto>()).Select(s => s == null ? null : new LocalizedSectionText(s.Id,
            (s.SectionElements ?? new List<FarsiSectionElementEditDto>()).Select(e => e == null ? null : new LocalizedElementText(e.Id, e.TinyText, e.EditorText)).ToList())).ToList());
}
