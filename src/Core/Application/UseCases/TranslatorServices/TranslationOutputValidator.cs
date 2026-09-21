using System;
using System.Linq;
using System.Text.RegularExpressions;
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
            var originalTags = ExtractHtmlTags((string)original);
            var translatedTags = ExtractHtmlTags((string)translated);
            if (!originalTags.SequenceEqual(translatedTags, StringComparer.Ordinal))
                return $"HTML markup changed in field '{fieldName}'.";
        }

        return null;
    }

    private static string[] ExtractHtmlTags(string text)
    {
        return Regex.Matches(text, "<[^>]+>")
            .Select(m => Regex.Replace(m.Value, "\\s+", " ").Trim())
            .ToArray();
    }
}
