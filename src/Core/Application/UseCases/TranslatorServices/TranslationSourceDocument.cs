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

    // The LocalizedTextJson payload for a manually translated Content graph (e.g. the legacy
    // FarsiContent editor's), or null unless it has exactly master's metadata/section/element IDs
    // and tree, string-or-null text and master's HTML structure wherever both have text.
    // Section order is taken from master, so only the ID sets have to match.
    public static string ToLocalizedTextJson(Content master, Content translated)
    {
        if (translated == null)
            return null;

        var masterPriorities = (master.Sections ?? Enumerable.Empty<ContentSection>()).ToDictionary(s => s.Id, s => s.Priority);
        var metadata = translated.Metadata;
        // The editor posts an Id-0 metadata node when master has none; it's only droppable empty.
        if (master.Metadata == null && metadata is { Id: 0, Title: null, Author: null, Keywords: null, Description: null })
            metadata = null;

        var projected = new Content
        {
            Id = translated.Id, Title = translated.Title, HeadLine = translated.HeadLine, Abstract = translated.Abstract,
            Description = translated.Description, Metadata = metadata,
            Sections = (translated.Sections ?? Enumerable.Empty<ContentSection>())
                .Select(s => new ContentSection
                {
                    Id = s.Id,
                    Priority = masterPriorities.TryGetValue(s.Id, out var priority) ? priority : int.MaxValue,
                    Elements = s.Elements
                })
                .ToList()
        };

        var document = Serialize(master);
        var translatedJson = Serialize(projected);
        return TranslationOutputValidator.Validate(document, translatedJson, TranslatableFields, allowNullText: true) == null
            ? ToLocalizedText(translatedJson)
            : null;
    }

    // The LocalizedTextJson payload for a provider response, or null unless the response is an
    // exact translation of document: same fields and tree, IDs unchanged, text string-or-null
    // with null-ness and HTML structure preserved.
    public static string ToLocalizedTextJson(string document, string translatedJson)
    {
        if (translatedJson == null || TranslationOutputValidator.Validate(document, translatedJson, TranslatableFields) != null)
            return null;

        return ToLocalizedText(translatedJson);
    }

    private static string ToLocalizedText(string translatedJson)
    {
        var translated = JsonSerializer.Deserialize<Document>(translatedJson, Options);
        return LegacyFarsiContentParser.Serialize(new LocalizedContentText(translated.Title, translated.HeadLine, translated.Abstract,
            translated.Description, translated.Metadata, translated.Sections));
    }
}
