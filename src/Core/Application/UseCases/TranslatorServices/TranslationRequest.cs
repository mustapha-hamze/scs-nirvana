using System.Collections.Generic;

namespace Application.UseCases.TranslatorServices;

// The port takes an opaque JSON payload rather than the Content entity itself, so the port
// contract doesn't depend on Content's shape — the caller decides what to serialize.
// TranslatableFields/TargetLanguage default (null) to the legacy Farsi full-document contract
// (TranslationOutputValidator.DefaultTranslatableFields, Persian).
public record TranslationRequest(string ContentJson, IReadOnlyCollection<string> TranslatableFields = null, string TargetLanguage = null);
