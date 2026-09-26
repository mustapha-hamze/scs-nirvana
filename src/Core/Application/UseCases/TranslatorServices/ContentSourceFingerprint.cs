using System.Collections.Generic;
using System.Linq;
using Cms.ContentLocalization;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// Deterministic fingerprint of the translatable source of a Content, compared against
// ContentTranslation.SourceFingerprint to detect stale translations. The canonical shape and hash
// live in the shared ContentLocalization/ContentLocalization.cs (SourceFingerprint), which the
// Content Delivery adapter compiles too, so both always agree.
public static class ContentSourceFingerprint
{
    // Returns lowercase 64-char hex SHA-256 of the UTF-8 canonical JSON. Expects content with
    // Metadata and Sections/Elements loaded (soft-deleted rows already excluded).
    public static string Compute(Content content) => SourceFingerprint.Compute(ToSource(content));

    internal static SourceContent ToSource(Content content)
    {
        var metadata = content.Metadata;
        return new SourceContent(content.Id, content.TypeId, content.Title, content.HeadLine, content.Abstract, content.Description,
            metadata == null ? null : new SourceMetadata(metadata.Id, metadata.Title, metadata.Author, metadata.Keywords, metadata.Description),
            (content.Sections ?? Enumerable.Empty<ContentSection>())
                .Select(s => new SourceSection(s.Id, s.Priority, s.IsActive,
                    (s.Elements ?? Enumerable.Empty<SectionElement>())
                        .Select(e => new SourceElement(e.Id, e.ElementType, e.Size, e.IsActive, e.TinyText, e.EditorText))
                        .ToList()))
                .ToList());
    }
}
