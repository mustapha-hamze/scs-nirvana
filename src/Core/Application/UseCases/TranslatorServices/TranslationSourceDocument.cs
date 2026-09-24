using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// The text-only document a background translation job sends to the provider: the content Id plus
// exactly the LocalizedContentText shape (stable metadata/section/element IDs and their text).
// Images, files, galleries, ElementTitle, relations, audit/state fields and FarsiContent are never
// part of it. Property names are PascalCase to match TranslationOutputValidator's field names.
public static class TranslationSourceDocument
{
    public static readonly IReadOnlyCollection<string> TranslatableFields = new[]
    {
        "Title", "HeadLine", "Abstract", "Description", "Author", "Keywords", "TinyText", "EditorText"
    };

    private record Document(int Id, string Title, string HeadLine, string Abstract, string Description,
        LocalizedMetadataText Metadata, List<LocalizedSectionText> Sections);

    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    // Expects content with Metadata and Sections/Elements loaded (soft-deleted rows excluded).
    // Sections/elements follow ContentSourceFingerprint's order.
    public static string Serialize(Content content)
    {
        var metadata = content.Metadata;
        return JsonSerializer.Serialize(new Document(content.Id, content.Title, content.HeadLine, content.Abstract, content.Description,
            metadata == null ? null : new LocalizedMetadataText(metadata.Id, metadata.Title, metadata.Author, metadata.Keywords, metadata.Description),
            (content.Sections ?? Enumerable.Empty<ContentSection>())
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .Select(s => new LocalizedSectionText(s.Id, (s.Elements ?? Enumerable.Empty<SectionElement>())
                    .OrderBy(e => e.Id)
                    .Select(e => new LocalizedElementText(e.Id, e.TinyText, e.EditorText))
                    .ToList()))
                .ToList()), Options);
    }

    // The LocalizedTextJson payload for a provider response, or null unless the response is an
    // exact translation of document: same fields and tree, IDs unchanged, text string-or-null
    // with null-ness and HTML structure preserved.
    public static string ToLocalizedTextJson(string document, string translatedJson)
    {
        if (translatedJson == null || TranslationOutputValidator.Validate(document, translatedJson, TranslatableFields) != null)
            return null;

        var translated = JsonSerializer.Deserialize<Document>(translatedJson, Options);
        return LegacyFarsiContentParser.Serialize(new LocalizedContentText(translated.Title, translated.HeadLine, translated.Abstract,
            translated.Description, translated.Metadata, translated.Sections));
    }
}
