using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// Deterministic fingerprint of the translatable source of a Content: the exact text a
// translation is produced from plus the structure it is laid out in. Compared against
// ContentTranslation.SourceFingerprint to detect stale translations.
//
// The canonical document is an explicit whitelist. Audit timestamps, legacy Status,
// FarsiContent, images, FileNameText, GalleryImages, ElementTitle, categories/tags/cultures and
// attachments are deliberately left out, so changing only those never marks a translation stale.
// Any change to this shape changes every fingerprint - bump Version when doing so.
public static class ContentSourceFingerprint
{
    private const int Version = 1;

    private record CanonicalContent(int Version, int ContentId, int TypeId, string Title, string HeadLine,
        string Abstract, string Description, CanonicalMetadata Metadata, List<CanonicalSection> Sections);

    private record CanonicalMetadata(int Id, string Title, string Author, string Keywords, string Description);

    private record CanonicalSection(int Id, int Priority, bool IsActive, List<CanonicalElement> Elements);

    private record CanonicalElement(int Id, int ElementType, int Size, bool IsActive, string TinyText, string EditorText);

    // Returns lowercase 64-char hex SHA-256 of the UTF-8 canonical JSON. Expects content with
    // Metadata and Sections/Elements loaded (soft-deleted rows already excluded).
    public static string Compute(Content content)
    {
        var metadata = content.Metadata;
        var document = new CanonicalContent(
            Version,
            content.Id,
            content.TypeId,
            content.Title,
            content.HeadLine,
            content.Abstract,
            content.Description,
            metadata == null ? null : new CanonicalMetadata(metadata.Id, metadata.Title, metadata.Author, metadata.Keywords, metadata.Description),
            (content.Sections ?? Enumerable.Empty<ContentSection>())
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .Select(s => new CanonicalSection(s.Id, s.Priority, s.IsActive,
                    (s.Elements ?? Enumerable.Empty<SectionElement>())
                        .OrderBy(e => e.Id)
                        .Select(e => new CanonicalElement(e.Id, e.ElementType, e.Size, e.IsActive, e.TinyText, e.EditorText))
                        .ToList()))
                .ToList());

        // Default options: fixed property order (declaration order), no indentation, and
        // lossless escaping - so equal documents always serialize to identical bytes.
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document)));
    }
}
