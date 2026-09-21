using System;
using System.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Application.UseCases.TranslatorServices;

// Deterministic structural validation of a translation model's JSON response against the JSON
// document that was submitted for translation. Syntax-only ("is this valid JSON?") acceptance
// isn't enough: the model must also preserve the document's exact shape and every field the
// prompt marks as non-translatable, and may only vary the handful of fields the prompt allows -
// and even those only as string/null text (with HTML structure intact).
//
// This mirrors the ALLOWED/PROTECTED field lists baked into OpenAiTranslationPort's prompt.
// They're duplicated rather than shared so this validator has no dependency on the prompt's exact
// wording; both lists should be kept in sync if the prompt's field lists ever change.
public static class TranslationOutputValidator
{
    private static readonly string[] TranslatableFields =
    {
        "Title", "HeadLine", "Abstract", "Description", "TinyText", "EditorText"
    };

    // Returns null when the translated document is valid, otherwise a human-readable (and
    // secret-safe: no field values, only field names/paths/types) description of the first
    // violation found.
    public static string Validate(string originalJson, string translatedJson)
    {
        JToken original;
        JToken translated;
        try
        {
            original = JToken.Parse(originalJson);
            translated = JToken.Parse(translatedJson);
        }
        catch (JsonException)
        {
            return "Model response was not valid JSON.";
        }

        return CompareTokens(original, translated, "$");
    }

    private static string CompareTokens(JToken original, JToken translated, string path)
    {
        if (original.Type != translated.Type)
            return $"Type changed at '{path}': expected {original.Type}, got {translated.Type}.";

        return original.Type switch
        {
            JTokenType.Object => CompareObjects((JObject)original, (JObject)translated, path),
            JTokenType.Array => CompareArrays((JArray)original, (JArray)translated, path),
            _ => CompareLeaf(original, translated, path)
        };
    }

    private static string CompareObjects(JObject original, JObject translated, string path)
    {
        var originalNames = original.Properties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var translatedNames = translated.Properties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        var missing = originalNames.Except(translatedNames).ToList();
        if (missing.Count > 0)
            return $"Missing field(s) at '{path}': {string.Join(", ", missing)}.";

        var extra = translatedNames.Except(originalNames).ToList();
        if (extra.Count > 0)
            return $"Unexpected field(s) at '{path}': {string.Join(", ", extra)}.";

        foreach (var property in original.Properties())
        {
            var error = CompareTokens(property.Value, translated[property.Name], $"{path}.{property.Name}");
            if (error != null)
                return error;
        }

        return null;
    }

    private static string CompareArrays(JArray original, JArray translated, string path)
    {
        if (original.Count != translated.Count)
            return $"Array length changed at '{path}': expected {original.Count}, got {translated.Count}.";

        for (var i = 0; i < original.Count; i++)
        {
            var error = CompareTokens(original[i], translated[i], $"{path}[{i}]");
            if (error != null)
                return error;
        }

        return null;
    }

    private static string CompareLeaf(JToken original, JToken translated, string path)
    {
        var fieldName = path[(path.LastIndexOf('.') + 1)..];

        if (!Array.Exists(TranslatableFields, f => f == fieldName))
        {
            // Protected by default: any field not explicitly allowed to change (IDs, dates,
            // booleans, numbers, and every other named field) must be byte-for-byte unchanged.
            return JToken.DeepEquals(original, translated)
                ? null
                : $"Protected field '{fieldName}' was changed.";
        }

        if (translated.Type != JTokenType.String && translated.Type != JTokenType.Null)
            return $"Field '{fieldName}' must be a string or null.";

        if (original.Type == JTokenType.Null != (translated.Type == JTokenType.Null))
            return $"Field '{fieldName}' changed between null and non-null.";

        if (original.Type == JTokenType.String)
        {
            var htmlError = CompareHtml((string)original, (string)translated);
            if (htmlError != null)
                return $"HTML markup changed in field '{fieldName}': {htmlError}";
        }

        return null;
    }

    // Parses both strings as HTML fragments (via HtmlAgilityPack, a lenient real parser - never
    // regex) and compares their markup structure while allowing text-node content to differ.
    // Element names, attributes/values, nesting/order, void/empty elements, and comments must
    // all be identical; only the visible text inside elements may have been translated.
    private static string CompareHtml(string originalHtml, string translatedHtml)
    {
        HtmlNodeCollection originalNodes;
        HtmlNodeCollection translatedNodes;
        try
        {
            var originalDoc = new HtmlDocument();
            originalDoc.LoadHtml(originalHtml);
            originalNodes = originalDoc.DocumentNode.ChildNodes;

            var translatedDoc = new HtmlDocument();
            translatedDoc.LoadHtml(translatedHtml);
            translatedNodes = translatedDoc.DocumentNode.ChildNodes;
        }
        catch (Exception)
        {
            return "could not parse HTML.";
        }

        return CompareNodeSequences(originalNodes, translatedNodes);
    }

    private static string CompareNodeSequences(HtmlNodeCollection original, HtmlNodeCollection translated)
    {
        if (original.Count != translated.Count)
            return $"child count changed (expected {original.Count}, got {translated.Count}).";

        for (var i = 0; i < original.Count; i++)
        {
            var error = CompareNodes(original[i], translated[i]);
            if (error != null)
                return error;
        }

        return null;
    }

    private static string CompareNodes(HtmlNode original, HtmlNode translated)
    {
        if (original.NodeType != translated.NodeType)
            return $"node type changed (expected {original.NodeType}, got {translated.NodeType}).";

        switch (original.NodeType)
        {
            case HtmlNodeType.Text:
                // Visible text is exactly what translation is allowed to change.
                return null;

            case HtmlNodeType.Comment:
                // Treated conservatively: comments and any other non-element/non-text markup
                // must remain byte-for-byte unchanged.
                return original.OuterHtml == translated.OuterHtml
                    ? null
                    : "comment or non-text markup changed.";

            case HtmlNodeType.Element:
                if (!string.Equals(original.Name, translated.Name, StringComparison.OrdinalIgnoreCase))
                    return $"element renamed from '{original.Name}' to '{translated.Name}'.";

                var attributeError = CompareAttributes(original, translated);
                if (attributeError != null)
                    return $"attributes changed on '<{original.Name}>': {attributeError}";

                return CompareNodeSequences(original.ChildNodes, translated.ChildNodes);

            default:
                return null;
        }
    }

    private static string CompareAttributes(HtmlNode original, HtmlNode translated)
    {
        var originalAttrs = original.Attributes.ToDictionary(a => a.Name, a => a.Value, StringComparer.OrdinalIgnoreCase);
        var translatedAttrs = translated.Attributes.ToDictionary(a => a.Name, a => a.Value, StringComparer.OrdinalIgnoreCase);

        var missing = originalAttrs.Keys.Except(translatedAttrs.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
            return $"missing attribute(s) {string.Join(", ", missing)}.";

        var extra = translatedAttrs.Keys.Except(originalAttrs.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        if (extra.Count > 0)
            return $"unexpected attribute(s) {string.Join(", ", extra)}.";

        foreach (var (name, value) in originalAttrs)
        {
            if (translatedAttrs[name] != value)
                return $"value of attribute '{name}' changed.";
        }

        return null;
    }
}
