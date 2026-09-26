#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Cms.ContentLocalization;

// The single authority for the source fingerprint and the localized-text payload, shared as a
// linked source file by Application (translation workflow, localized reads) and the Content
// Delivery SQL adapter, so both hash and parse exactly alike without either layer referencing the
// other. Framework-only on purpose: no Domain, EF or package types. Everything is internal to
// whichever assembly compiles it.

// The translatable source of a content item: the non-deleted master graph (inactive sections and
// elements included), in any order.
internal sealed record SourceContent(int Id, int TypeId, string Title, string HeadLine, string Abstract, string Description,
    SourceMetadata Metadata, IReadOnlyList<SourceSection> Sections);

internal sealed record SourceMetadata(int Id, string Title, string Author, string Keywords, string Description);

internal sealed record SourceSection(int Id, int Priority, bool IsActive, IReadOnlyList<SourceElement> Elements);

internal sealed record SourceElement(int Id, int ElementType, int Size, bool IsActive, string TinyText, string EditorText);

// ContentTranslation.LocalizedTextJson payload. Property names and order are the stored JSON
// shape - they must stay identical to Application's public LocalizedContentText records.
internal sealed record LocalizedText(string Title, string HeadLine, string Abstract, string Description,
    LocalizedTextMetadata Metadata, List<LocalizedTextSection> Sections);

internal sealed record LocalizedTextMetadata(int Id, string Title, string Author, string Keywords, string Description);

internal sealed record LocalizedTextSection(int Id, List<LocalizedTextElement> Elements);

internal sealed record LocalizedTextElement(int Id, string TinyText, string EditorText);

// Deterministic fingerprint of the translatable source: the exact text a translation is produced
// from plus the structure it is laid out in. Compared against ContentTranslation.SourceFingerprint
// to detect stale translations.
//
// The canonical document is an explicit whitelist. Audit timestamps, legacy Status,
// FarsiContent, images, FileNameText, GalleryImages, ElementTitle, categories/tags/cultures and
// attachments are deliberately left out, so changing only those never marks a translation stale.
// Any change to this shape changes every fingerprint - bump Version when doing so.
internal static class SourceFingerprint
{
    // TranslationStatus.Ready as stored in CMS_ContentTranslations.TranslationStatus.
    public const byte ReadyStatus = 3;

    private const int Version = 1;

    private record CanonicalContent(int Version, int ContentId, int TypeId, string Title, string HeadLine,
        string Abstract, string Description, CanonicalMetadata Metadata, List<CanonicalSection> Sections);

    private record CanonicalMetadata(int Id, string Title, string Author, string Keywords, string Description);

    private record CanonicalSection(int Id, int Priority, bool IsActive, List<CanonicalElement> Elements);

    private record CanonicalElement(int Id, int ElementType, int Size, bool IsActive, string TinyText, string EditorText);

    // Lowercase 64-char hex SHA-256 of the UTF-8 canonical JSON.
    public static string Compute(SourceContent content)
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
            (content.Sections ?? Enumerable.Empty<SourceSection>())
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .Select(s => new CanonicalSection(s.Id, s.Priority, s.IsActive,
                    (s.Elements ?? Enumerable.Empty<SourceElement>())
                        .OrderBy(e => e.Id)
                        .Select(e => new CanonicalElement(e.Id, e.ElementType, e.Size, e.IsActive, e.TinyText, e.EditorText))
                        .ToList()))
                .ToList());

        // Default options: fixed property order (declaration order), no indentation, and
        // lossless escaping - so equal documents always serialize to identical bytes.
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document)));
    }
}

// Strict parsers for the stored payload and the legacy Content.FarsiContent snapshot (a
// Newtonsoft-serialized Content graph). Read JSON directly - never deserialize into an entity.
// Property names match case-insensitively; everything not whitelisted is ignored.
internal static class LocalizedTextParser
{
    public const string InvalidJson = "invalid_json";
    public const string RootNotObject = "root_not_object";
    public const string InvalidShape = "invalid_shape";
    public const string DuplicateProperty = "duplicate_property";
    public const string MissingId = "missing_id";
    public const string InvalidId = "invalid_id";
    public const string DuplicateId = "duplicate_id";
    public const string IdMismatch = "id_mismatch";
    public const string InvalidText = "invalid_text";
    public const string MissingProperty = "missing_property";

    // Keeps Farsi readable (not \u-escaped) while still escaping HTML-sensitive characters.
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    // Exactly one of LocalizedTextJson / ReasonCode is non-null.
    public record Result(string LocalizedTextJson, string ReasonCode);

    private sealed class Rejected(string reasonCode) : Exception(reasonCode)
    {
        public string ReasonCode { get; } = reasonCode;
    }

    // Legacy snapshot validated against the current master: every metadata/section/element ID
    // must match the same master node (and any ContentId/SectionId it carries must match its
    // parent). Master nodes missing from the snapshot are simply untranslated.
    public static Result ParseLegacy(string legacyJson, SourceContent master)
    {
        var (text, reasonCode) = ExtractLegacy(legacyJson, master);
        return new Result(text == null ? null : JsonSerializer.Serialize(text, JsonOptions), reasonCode);
    }

    // ParseLegacy's text itself; exactly one of Text / ReasonCode is non-null.
    public static (LocalizedText Text, string ReasonCode) ExtractLegacy(string legacyJson, SourceContent master)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(legacyJson);
        }
        catch (JsonException)
        {
            return (null, InvalidJson);
        }

        using (document)
        {
            try
            {
                return (Extract(document.RootElement, master), null);
            }
            catch (Rejected rejected)
            {
                return (null, rejected.ReasonCode);
            }
        }
    }

    // Strict read of a stored LocalizedTextJson payload: every field present, text a string or an
    // explicit null, metadata an object or null, sections/elements arrays of objects, integer IDs
    // unique per node kind. Not checked against any master - a stale payload may lack newer nodes
    // or keep removed ones. Null when unusable.
    public static LocalizedText ReadStored(string localizedTextJson)
    {
        if (string.IsNullOrWhiteSpace(localizedTextJson))
            return null;
        try
        {
            using var document = JsonDocument.Parse(localizedTextJson);
            return ReadStored(document.RootElement);
        }
        catch (Exception e) when (e is JsonException or Rejected)
        {
            return null;
        }
    }

    // A stored payload that is safe to serve over master: ReadStored plus exactly master's
    // metadata, section and per-section element IDs. A matching fingerprint should imply the
    // shape; checked anyway so a corrupt payload is never half-applied.
    public static LocalizedText ReadCurrent(string localizedTextJson, SourceContent master) =>
        ReadStored(localizedTextJson) is { } text && MatchesMaster(text, master) ? text : null;

    private static bool MatchesMaster(LocalizedText text, SourceContent master)
    {
        if (text.Metadata?.Id != master.Metadata?.Id)
            return false;
        var masterSections = (master.Sections ?? Enumerable.Empty<SourceSection>()).ToList();
        if (text.Sections.Count != masterSections.Count)
            return false;

        var sections = text.Sections.ToDictionary(s => s.Id);
        return masterSections.All(s => sections.TryGetValue(s.Id, out var translated)
            && translated.Elements.Select(e => e.Id).ToHashSet().SetEquals((s.Elements ?? Enumerable.Empty<SourceElement>()).Select(e => e.Id)));
    }

    private static LocalizedText ReadStored(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new Rejected(RootNotObject);

        var metadata = Required(root, "Metadata");
        if (metadata.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
            throw new Rejected(InvalidShape);

        var sectionIds = new HashSet<int>();
        var elementIds = new HashSet<int>();
        return new LocalizedText(RequiredText(root, "Title"), RequiredText(root, "HeadLine"), RequiredText(root, "Abstract"), RequiredText(root, "Description"),
            metadata.ValueKind == JsonValueKind.Null ? null
                : new LocalizedTextMetadata(RequiredId(metadata), RequiredText(metadata, "Title"), RequiredText(metadata, "Author"),
                    RequiredText(metadata, "Keywords"), RequiredText(metadata, "Description")),
            Objects(Required(root, "Sections")).Select(section => new LocalizedTextSection(UniqueId(section, sectionIds),
                Objects(Required(section, "Elements")).Select(element => new LocalizedTextElement(UniqueId(element, elementIds),
                    RequiredText(element, "TinyText"), RequiredText(element, "EditorText"))).ToList())).ToList());
    }

    // Tolerant read of a legacy snapshot for seeding the manual editor: text keyed by the
    // snapshot's own IDs, whatever the current master looks like - the editor aligns it and drops
    // removed nodes. An explicit null is the translator's intentional blank; an omitted text
    // property (a partial snapshot) takes master's current text for the same node, so saving it
    // unchanged never turns an accidental gap into a blank. Null unless it's a JSON object
    // snapshot of master.
    public static LocalizedText ReadLegacy(string legacyJson, SourceContent master)
    {
        if (string.IsNullOrWhiteSpace(legacyJson))
            return null;
        try
        {
            using var document = JsonDocument.Parse(legacyJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || RequiredId(root) != master.Id)
                return null;

            var masterElements = new Dictionary<(int, int), SourceElement>();
            foreach (var section in master.Sections ?? Enumerable.Empty<SourceSection>())
                foreach (var element in section.Elements ?? Enumerable.Empty<SourceElement>())
                    masterElements[(section.Id, element.Id)] = element;

            return new LocalizedText(SeedText(root, "Title", master.Title), SeedText(root, "HeadLine", master.HeadLine),
                SeedText(root, "Abstract", master.Abstract), SeedText(root, "Description", master.Description),
                NonNull(root, "Metadata") is { } metadata ? ReadLegacyMetadata(metadata, master.Metadata) : null,
                NonNull(root, "Sections") is { } sections
                    ? Objects(sections).Select(section =>
                    {
                        var sectionId = RequiredId(section);
                        return new LocalizedTextSection(sectionId, NonNull(section, "Elements") is { } elements
                            ? Objects(elements).Select(e =>
                            {
                                var id = RequiredId(e);
                                var source = masterElements.GetValueOrDefault((sectionId, id));
                                return new LocalizedTextElement(id, SeedText(e, "TinyText", source?.TinyText), SeedText(e, "EditorText", source?.EditorText));
                            }).ToList()
                            : new List<LocalizedTextElement>());
                    }).ToList()
                    : new List<LocalizedTextSection>());
        }
        // InvalidOperationException: a non-object where an object was expected.
        catch (Exception e) when (e is JsonException or Rejected or InvalidOperationException)
        {
            return null;
        }
    }

    private static LocalizedTextMetadata ReadLegacyMetadata(JsonElement json, SourceMetadata master)
    {
        var id = RequiredId(json);
        var source = master?.Id == id ? master : null;
        return new LocalizedTextMetadata(id, SeedText(json, "Title", source?.Title), SeedText(json, "Author", source?.Author),
            SeedText(json, "Keywords", source?.Keywords), SeedText(json, "Description", source?.Description));
    }

    // Omitted -> source; explicit null -> null; otherwise must be a string.
    private static string SeedText(JsonElement json, string name, string source) => Find(json, name) switch
    {
        null => source,
        { ValueKind: JsonValueKind.Null } => null,
        { ValueKind: JsonValueKind.String } value => value.GetString(),
        _ => throw new Rejected(InvalidText)
    };

    private static JsonElement Required(JsonElement json, string name) => Find(json, name) ?? throw new Rejected(MissingProperty);

    // Distinguishes an explicit null (kept) from a missing property (rejected).
    private static string RequiredText(JsonElement json, string name) => Required(json, name) switch
    {
        { ValueKind: JsonValueKind.Null } => null,
        { ValueKind: JsonValueKind.String } value => value.GetString(),
        _ => throw new Rejected(InvalidText)
    };

    private static int UniqueId(JsonElement json, HashSet<int> seen)
    {
        var id = RequiredId(json);
        return seen.Add(id) ? id : throw new Rejected(DuplicateId);
    }

    private static LocalizedText Extract(JsonElement root, SourceContent master)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new Rejected(RootNotObject);
        if (RequiredId(root) != master.Id)
            throw new Rejected(IdMismatch);

        return new LocalizedText(Text(root, "Title"), Text(root, "HeadLine"), Text(root, "Abstract"), Text(root, "Description"),
            NonNull(root, "Metadata") is { } metadata ? ExtractMetadata(metadata, master) : null,
            NonNull(root, "Sections") is { } sections ? ExtractSections(sections, master) : new List<LocalizedTextSection>());
    }

    private static LocalizedTextMetadata ExtractMetadata(JsonElement json, SourceContent master)
    {
        if (json.ValueKind != JsonValueKind.Object)
            throw new Rejected(InvalidShape);
        var id = RequiredId(json);
        if (master.Metadata == null || id != master.Metadata.Id)
            throw new Rejected(IdMismatch);
        RequireParent(json, "ContentId", master.Id);

        return new LocalizedTextMetadata(id, Text(json, "Title"), Text(json, "Author"), Text(json, "Keywords"), Text(json, "Description"));
    }

    private static List<LocalizedTextSection> ExtractSections(JsonElement json, SourceContent master)
    {
        var masterSections = (master.Sections ?? Enumerable.Empty<SourceSection>()).ToDictionary(s => s.Id);
        var seenSections = new HashSet<int>();
        var seenElements = new HashSet<int>();
        var result = new List<LocalizedTextSection>();

        foreach (var sectionJson in Objects(json))
        {
            var sectionId = RequiredId(sectionJson);
            if (!seenSections.Add(sectionId))
                throw new Rejected(DuplicateId);
            if (!masterSections.TryGetValue(sectionId, out var masterSection))
                throw new Rejected(IdMismatch);
            RequireParent(sectionJson, "ContentId", master.Id);

            var masterElementIds = (masterSection.Elements ?? Enumerable.Empty<SourceElement>()).Select(e => e.Id).ToHashSet();
            var elements = new List<LocalizedTextElement>();
            if (NonNull(sectionJson, "Elements") is { } elementsJson)
            {
                foreach (var elementJson in Objects(elementsJson))
                {
                    var elementId = RequiredId(elementJson);
                    if (!seenElements.Add(elementId))
                        throw new Rejected(DuplicateId);
                    if (!masterElementIds.Contains(elementId))
                        throw new Rejected(IdMismatch);
                    RequireParent(elementJson, "SectionId", sectionId);

                    elements.Add(new LocalizedTextElement(elementId, Text(elementJson, "TinyText"), Text(elementJson, "EditorText")));
                }
            }

            result.Add(new LocalizedTextSection(sectionId, elements));
        }

        return result;
    }

    // Array whose items are all objects.
    private static IEnumerable<JsonElement> Objects(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
            throw new Rejected(InvalidShape);
        var items = json.EnumerateArray().ToList();
        if (items.Any(i => i.ValueKind != JsonValueKind.Object))
            throw new Rejected(InvalidShape);
        return items;
    }

    // Case-insensitive lookup; two properties differing only by case are ambiguous.
    private static JsonElement? Find(JsonElement json, string name)
    {
        JsonElement? found = null;
        foreach (var property in json.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (found != null)
                throw new Rejected(DuplicateProperty);
            found = property.Value;
        }

        return found;
    }

    private static JsonElement? NonNull(JsonElement json, string name) =>
        Find(json, name) is { ValueKind: not JsonValueKind.Null } value ? value : null;

    private static int RequiredId(JsonElement json)
    {
        var value = NonNull(json, "Id") ?? throw new Rejected(MissingId);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var id))
            throw new Rejected(InvalidId);
        return id;
    }

    // An absent/null parent reference is tolerated; a present one must match.
    private static void RequireParent(JsonElement json, string name, int expectedId)
    {
        if (NonNull(json, name) is { } value && (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var id) || id != expectedId))
            throw new Rejected(IdMismatch);
    }

    private static string Text(JsonElement json, string name) => NonNull(json, name) switch
    {
        null => null,
        { ValueKind: JsonValueKind.String } value => value.GetString(),
        _ => throw new Rejected(InvalidText)
    };
}
