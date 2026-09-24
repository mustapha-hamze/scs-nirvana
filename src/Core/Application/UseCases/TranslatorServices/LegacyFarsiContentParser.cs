using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// ContentTranslation.LocalizedTextJson payload: only translatable text, keyed by the stable
// master-graph IDs it applies to. Layout, images, files, galleries, ElementTitle, relations and
// audit/state fields stay in the master Content graph.
public record LocalizedContentText(string Title, string HeadLine, string Abstract, string Description,
    LocalizedMetadataText Metadata, List<LocalizedSectionText> Sections);

public record LocalizedMetadataText(int Id, string Title, string Author, string Keywords, string Description);

public record LocalizedSectionText(int Id, List<LocalizedElementText> Elements);

public record LocalizedElementText(int Id, string TinyText, string EditorText);

// Pure parser from a legacy Content.FarsiContent snapshot (a Newtonsoft-serialized Content graph,
// see FarsiContentMapper.SerializeForStorage) to LocalizedContentText, validated against the
// current non-deleted master graph. Reads JSON directly - never deserializes into an entity.
// Property names match case-insensitively; everything it doesn't whitelist is ignored.
//
// Accepted: every legacy metadata/section/element ID must match the same node in the master
// graph (and any ContentId/SectionId it carries must match its parent). Master nodes missing from
// the snapshot are simply untranslated. Anything else is rejected with a reason code.
public static class LegacyFarsiContentParser
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

    // Keeps Farsi readable (not \u-escaped) while still escaping HTML-sensitive characters.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    // Exactly one of LocalizedTextJson / ReasonCode is non-null.
    public record Result(string LocalizedTextJson, string ReasonCode);

    private sealed class Rejected(string reasonCode) : Exception(reasonCode)
    {
        public string ReasonCode { get; } = reasonCode;
    }

    public static Result Parse(string legacyJson, Content master)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(legacyJson);
        }
        catch (JsonException)
        {
            return new Result(null, InvalidJson);
        }

        using (document)
        {
            try
            {
                return new Result(JsonSerializer.Serialize(Extract(document.RootElement, master), Options), null);
            }
            catch (Rejected rejected)
            {
                return new Result(null, rejected.ReasonCode);
            }
        }
    }

    public static LocalizedContentText Deserialize(string localizedTextJson) =>
        JsonSerializer.Deserialize<LocalizedContentText>(localizedTextJson, Options);

    private static LocalizedContentText Extract(JsonElement root, Content master)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new Rejected(RootNotObject);
        if (RequiredId(root) != master.Id)
            throw new Rejected(IdMismatch);

        return new LocalizedContentText(Text(root, "Title"), Text(root, "HeadLine"), Text(root, "Abstract"), Text(root, "Description"),
            NonNull(root, "Metadata") is { } metadata ? ExtractMetadata(metadata, master) : null,
            NonNull(root, "Sections") is { } sections ? ExtractSections(sections, master) : new List<LocalizedSectionText>());
    }

    private static LocalizedMetadataText ExtractMetadata(JsonElement json, Content master)
    {
        if (json.ValueKind != JsonValueKind.Object)
            throw new Rejected(InvalidShape);
        var id = RequiredId(json);
        if (master.Metadata == null || id != master.Metadata.Id)
            throw new Rejected(IdMismatch);
        RequireParent(json, "ContentId", master.Id);

        return new LocalizedMetadataText(id, Text(json, "Title"), Text(json, "Author"), Text(json, "Keywords"), Text(json, "Description"));
    }

    private static List<LocalizedSectionText> ExtractSections(JsonElement json, Content master)
    {
        var masterSections = (master.Sections ?? Enumerable.Empty<ContentSection>()).ToDictionary(s => s.Id);
        var seenSections = new HashSet<int>();
        var seenElements = new HashSet<int>();
        var result = new List<LocalizedSectionText>();

        foreach (var sectionJson in Objects(json))
        {
            var sectionId = RequiredId(sectionJson);
            if (!seenSections.Add(sectionId))
                throw new Rejected(DuplicateId);
            if (!masterSections.TryGetValue(sectionId, out var masterSection))
                throw new Rejected(IdMismatch);
            RequireParent(sectionJson, "ContentId", master.Id);

            var masterElementIds = (masterSection.Elements ?? Enumerable.Empty<SectionElement>()).Select(e => e.Id).ToHashSet();
            var elements = new List<LocalizedElementText>();
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

                    elements.Add(new LocalizedElementText(elementId, Text(elementJson, "TinyText"), Text(elementJson, "EditorText")));
                }
            }

            result.Add(new LocalizedSectionText(sectionId, elements));
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
