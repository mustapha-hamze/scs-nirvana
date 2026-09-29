using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Domains.Entities.ContentManagement;
using HtmlAgilityPack;

namespace Application.UseCases.TranslatorServices;

// The text-slot contract of a background translation job: the provider only ever sees an ordered
// list of plain text values and returns the same number of values in the same order. The server
// owns everything else - IDs, hierarchy, order, protected fields and HTML markup - and rebuilds
// LocalizedTextJson from the source document plus the returned values, bound by position alone.
//
// Slots follow TranslationSourceDocument's order (content Title/HeadLine/Abstract/Description,
// metadata, sections by Priority then Id, elements by Id). Null, empty and whitespace-only values
// get no slot and are kept as they are. A value with HTML markup (and every EditorText) is parsed
// with HtmlAgilityPack: each visible text node outside script/style becomes one slot, entity-decoded,
// and its translation is re-encoded and spliced back at the node's exact source offsets, so every
// other byte (tags, attributes, comments, script/style, whitespace around the text) is untouched.
public sealed class TranslationTextSlots
{
    // The single property of the provider's JSON response.
    public const string ResponseProperty = "translations";

    private static readonly HashSet<string> RawTextElements = new(StringComparer.OrdinalIgnoreCase) { "script", "style" };

    // Field: index into Fields(document); Start/Length: the replaced range of that field's raw value.
    private sealed record Slot(int Field, string Raw, bool Html, int Start, int Length);

    private readonly string _document;
    private readonly List<Slot> _slots;

    // Provider-facing text of each slot, in order.
    public IReadOnlyList<string> Texts { get; }

    private TranslationTextSlots(string document, List<Slot> slots)
    {
        _document = document;
        _slots = slots;
        Texts = slots.Select(s => s.Html ? HtmlEntity.DeEntitize(s.Raw.Substring(s.Start, s.Length)) : s.Raw.Substring(s.Start, s.Length)).ToList();
    }

    // Null when the source can't round-trip through its own slots (e.g. malformed markup, which the
    // final TranslationOutputValidator check rejects whatever the provider returns).
    public static TranslationTextSlots Build(Content content)
    {
        var document = TranslationSourceDocument.Serialize(content);
        var slots = new List<Slot>();
        var fields = Fields(JsonNode.Parse(document));
        for (var i = 0; i < fields.Count; i++)
            AddSlots(i, fields[i].Name, fields[i].Owner[fields[i].Name].GetValue<string>(), slots);
        var plan = new TranslationTextSlots(document, slots);
        return plan.ToLocalizedTextJson(plan.Texts) == null ? null : plan;
    }

    // Every non-null translatable string in document order: exactly TranslationSourceDocument's traversal.
    private static List<(JsonObject Owner, string Name)> Fields(JsonNode node, List<(JsonObject, string)> fields = null)
    {
        fields ??= new();
        if (node is JsonArray array)
        {
            foreach (var item in array)
                Fields(item, fields);
        }
        else if (node is JsonObject obj)
        {
            foreach (var (name, value) in obj)
            {
                if (value is JsonValue v && v.TryGetValue<string>(out _) && TranslationSourceDocument.TranslatableFields.Contains(name))
                    fields.Add((obj, name));
                else if (value is JsonObject or JsonArray)
                    Fields(value, fields);
            }
        }
        return fields;
    }

    private static void AddSlots(int field, string name, string raw, List<Slot> slots)
    {
        var html = new HtmlDocument();
        html.LoadHtml(raw);
        if (name != "EditorText" && html.DocumentNode.Descendants().All(n => n.NodeType == HtmlNodeType.Text))
        {
            AddTrimmed(field, raw, false, 0, raw.Length, slots);
            return;
        }

        foreach (var node in html.DocumentNode.Descendants())
        {
            if (node.NodeType == HtmlNodeType.Text && !node.Ancestors().Any(a => RawTextElements.Contains(a.Name)))
                AddTrimmed(field, raw, true, node.InnerStartIndex, node.InnerLength, slots);
        }
    }

    // Whitespace around a value stays server-owned; a range with no visible text gets no slot.
    private static void AddTrimmed(int field, string raw, bool html, int start, int length, List<Slot> slots)
    {
        var end = start + length;
        while (start < end && char.IsWhiteSpace(raw[start])) start++;
        while (end > start && char.IsWhiteSpace(raw[end - 1])) end--;
        var text = raw.Substring(start, end - start);
        if (!string.IsNullOrWhiteSpace(html ? HtmlEntity.DeEntitize(text) : text))
            slots.Add(new Slot(field, raw, html, start, end - start));
    }

    // The canonical LocalizedTextJson for translations of Texts (same count and order), or null
    // unless the rebuilt document passes TranslationOutputValidator against the source.
    public string ToLocalizedTextJson(IReadOnlyList<string> translations)
    {
        if (translations == null || translations.Count != _slots.Count || translations.Any(string.IsNullOrWhiteSpace))
            return null;

        var rebuilt = JsonNode.Parse(_document);
        var fields = Fields(rebuilt);
        var values = new Dictionary<int, string>();
        // Reverse order: each splice leaves the offsets of earlier slots in the same field valid.
        for (var i = _slots.Count - 1; i >= 0; i--)
        {
            var slot = _slots[i];
            var current = values.GetValueOrDefault(slot.Field, slot.Raw);
            var text = translations[i].Trim();
            if (slot.Html)
                text = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            values[slot.Field] = current[..slot.Start] + text + current[(slot.Start + slot.Length)..];
        }

        foreach (var (field, value) in values)
            fields[field].Owner[fields[field].Name] = value;

        return TranslationSourceDocument.ToLocalizedTextJson(_document, rebuilt.ToJsonString());
    }

    // Parses the provider's {"translations": [...]} response. Returns null and the translated values,
    // or a fixed error code: invalid_json for unparseable text, invalid_response for any other shape
    // (not an object, another or an extra property, not an array, wrong count, a non-string or blank item).
    public static string ParseResponse(string json, int expectedCount, out IReadOnlyList<string> translations)
    {
        translations = null;
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return ContentTranslationErrorCodes.InvalidJson;
        }

        using (parsed)
        {
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1
                || !root.TryGetProperty(ResponseProperty, out var array) || array.ValueKind != JsonValueKind.Array
                || array.GetArrayLength() != expectedCount
                || array.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(e.GetString())))
                return ContentTranslationErrorCodes.InvalidResponse;

            translations = array.EnumerateArray().Select(e => e.GetString()).ToList();
            return null;
        }
    }
}
