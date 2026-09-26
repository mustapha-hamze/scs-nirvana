using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cms.ContentLocalization;
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

// Parsers for the stored payload and the legacy Content.FarsiContent snapshot (a
// Newtonsoft-serialized Content graph, see FarsiContentMapper.SerializeForStorage). The rules live
// in the shared ContentLocalization/ContentLocalization.cs (LocalizedTextParser), which the Content
// Delivery adapter compiles too; this maps them onto the Domain graph and the public records.
public static class LegacyFarsiContentParser
{
    public const string InvalidJson = LocalizedTextParser.InvalidJson;
    public const string RootNotObject = LocalizedTextParser.RootNotObject;
    public const string InvalidShape = LocalizedTextParser.InvalidShape;
    public const string DuplicateProperty = LocalizedTextParser.DuplicateProperty;
    public const string MissingId = LocalizedTextParser.MissingId;
    public const string InvalidId = LocalizedTextParser.InvalidId;
    public const string DuplicateId = LocalizedTextParser.DuplicateId;
    public const string IdMismatch = LocalizedTextParser.IdMismatch;
    public const string InvalidText = LocalizedTextParser.InvalidText;
    public const string MissingProperty = LocalizedTextParser.MissingProperty;

    // Exactly one of LocalizedTextJson / ReasonCode is non-null.
    public record Result(string LocalizedTextJson, string ReasonCode);

    // Accepted: every legacy metadata/section/element ID must match the same node in the master
    // graph (and any ContentId/SectionId it carries must match its parent). Master nodes missing
    // from the snapshot are simply untranslated. Anything else is rejected with a reason code.
    public static Result Parse(string legacyJson, Content master)
    {
        var result = LocalizedTextParser.ParseLegacy(legacyJson, ContentSourceFingerprint.ToSource(master));
        return new Result(result.LocalizedTextJson, result.ReasonCode);
    }

    public static string Serialize(LocalizedContentText text) => JsonSerializer.Serialize(text, LocalizedTextParser.JsonOptions);

    public static LocalizedContentText Deserialize(string localizedTextJson) =>
        JsonSerializer.Deserialize<LocalizedContentText>(localizedTextJson, LocalizedTextParser.JsonOptions);

    // Strict read of a stored LocalizedTextJson payload (the shape Serialize writes). Not checked
    // against any master - a stale payload may lack newer nodes or keep removed ones. Null when
    // unusable.
    public static LocalizedContentText ReadStored(string localizedTextJson) => From(LocalizedTextParser.ReadStored(localizedTextJson));

    // ReadStored plus exactly master's metadata, section and per-section element IDs.
    internal static LocalizedContentText ReadCurrent(string localizedTextJson, Content master) =>
        From(LocalizedTextParser.ReadCurrent(localizedTextJson, ContentSourceFingerprint.ToSource(master)));

    // Tolerant read of a legacy snapshot for seeding the manual editor (see
    // LocalizedTextParser.ReadLegacy). Null unless it's a JSON object snapshot of master.
    public static LocalizedContentText ReadLegacy(string legacyJson, Content master) =>
        From(LocalizedTextParser.ReadLegacy(legacyJson, ContentSourceFingerprint.ToSource(master)));

    private static LocalizedContentText From(LocalizedText text) => text == null ? null
        : new LocalizedContentText(text.Title, text.HeadLine, text.Abstract, text.Description,
            text.Metadata == null ? null
                : new LocalizedMetadataText(text.Metadata.Id, text.Metadata.Title, text.Metadata.Author, text.Metadata.Keywords, text.Metadata.Description),
            text.Sections.Select(s => new LocalizedSectionText(s.Id,
                s.Elements.Select(e => new LocalizedElementText(e.Id, e.TinyText, e.EditorText)).ToList())).ToList());
}
