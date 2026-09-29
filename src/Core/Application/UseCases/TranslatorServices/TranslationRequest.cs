using System.Collections.Generic;

namespace Application.UseCases.TranslatorServices;

// The port takes an opaque JSON payload rather than the Content entity itself, so the port
// contract doesn't depend on Content's shape — the caller decides what to serialize.
// TranslatableFields/TargetLanguage default (null) to the legacy Farsi full-document contract
// (TranslationOutputValidator.DefaultTranslatableFields, Persian).
// Texts (background jobs, see TranslationTextSlots): the ordered plain values to translate instead
// of a document; the result then carries TranslatedTexts in the same order.
public record TranslationRequest(string ContentJson, IReadOnlyCollection<string> TranslatableFields = null, string TargetLanguage = null,
    IReadOnlyList<string> Texts = null)
{
    public static TranslationRequest ForTexts(IReadOnlyList<string> texts, string targetLanguage) =>
        new(null, TargetLanguage: targetLanguage, Texts: texts);
}
