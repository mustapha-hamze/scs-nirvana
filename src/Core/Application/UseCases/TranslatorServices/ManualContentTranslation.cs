using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

public enum ManualTranslationSaveResult
{
    Saved,
    NotFound,
    CultureUnavailable,

    // The edited text doesn't match the current master (stale or tampered IDs/tree, HTML, or text
    // in a field its element type doesn't edit).
    InvalidStructure,

    // The (content, culture) translation row is soft-deleted; it's never resurrected.
    TranslationDeleted,

    // A concurrent writer won; nothing was saved.
    Conflict,

    // The English source changed since the editor loaded (fingerprint mismatch); nothing was
    // saved - the editor must reload and review against the current source.
    SourceChanged
}

public enum ManualTranslationSeed
{
    Canonical,
    Legacy,
    Source
}

// What the manual editor renders: the current master (read-only layout, media and source text),
// the fingerprint to send back on save, the stored translation's status (null: none; Stale when
// its fingerprint is behind), and editable text aligned 1:1 to master's metadata/sections/elements.
public record ManualTranslationEditor(Content Source, string SourceFingerprint, TranslationStatus? TranslationStatus,
    ManualTranslationSeed Seed, LocalizedContentText Text);

// Manual (editor) translation: reads the editor model and saves the edited text as the activation
// culture's canonical ContentTranslation. Validates everything before writing, never calls the
// translation provider or queues work, and never touches the legacy Content.FarsiContent.
public class ManualContentTranslation
{
    public const string Provider = "manual";

    // Element types whose text lives in TinyText / EditorText; every other type (image, gallery,
    // file) has no editable text.
    private static readonly HashSet<int> TinyTextTypes = new() { 1000, 1006, 1007, 1008, 1009, 1010, 1011 };
    private static readonly HashSet<int> EditorTextTypes = new() { 1002, 1005 };

    private readonly IContentTranslationJobRepository _translations;
    private readonly TimeProvider _timeProvider;

    public ManualContentTranslation(IContentTranslationJobRepository translations, TimeProvider timeProvider)
    {
        _translations = translations;
        _timeProvider = timeProvider;
    }

    // Read-only: null when the content isn't the application's. Text is seeded from the usable
    // (non-deleted, structurally valid) canonical row whatever its status, else the legacy
    // FarsiContent snapshot, else the source itself.
    public async Task<ManualTranslationEditor> GetEditor(int contentId, int cultureId, int applicationId, CancellationToken cancellationToken = default)
    {
        if (!await _translations.ContentBelongsToApplication(contentId, applicationId, cancellationToken))
            return null;
        var master = await _translations.FindSourceGraph(contentId, cancellationToken);
        if (master == null)
            return null;

        var fingerprint = ContentSourceFingerprint.Compute(master);
        var translation = cultureId == 0 ? null : await _translations.FindTranslation(contentId, cultureId, cancellationToken);
        if (translation is { IsDeleted: true })
            translation = null;
        TranslationStatus? status = translation == null ? null
            : translation.SourceFingerprint == fingerprint ? translation.TranslationStatus : TranslationStatus.Stale;

        var (seed, text) = LegacyFarsiContentParser.ReadStored(translation?.LocalizedTextJson) is { } canonical ? (ManualTranslationSeed.Canonical, canonical)
            : LegacyFarsiContentParser.ReadLegacy(master.FarsiContent, contentId) is { } legacy ? (ManualTranslationSeed.Legacy, legacy)
            : (ManualTranslationSeed.Source, null);
        return new ManualTranslationEditor(master, fingerprint, status, seed, Align(master, text));
    }

    // Master's shape and order: seeded text where the seed has the same node (an element only in
    // the same section; the first of duplicate IDs wins), master's own text everywhere else.
    private static LocalizedContentText Align(Content master, LocalizedContentText seed)
    {
        var metadata = master.Metadata;
        var elements = new Dictionary<(int, int), LocalizedElementText>();
        foreach (var section in (seed?.Sections ?? new List<LocalizedSectionText>()).Where(s => s?.Elements != null))
            foreach (var element in section.Elements.Where(e => e != null))
                elements.TryAdd((section.Id, element.Id), element);

        return new LocalizedContentText(
            seed == null ? master.Title : seed.Title,
            seed == null ? master.HeadLine : seed.HeadLine,
            seed == null ? master.Abstract : seed.Abstract,
            seed == null ? master.Description : seed.Description,
            metadata == null ? null
                : seed?.Metadata is { } seeded && seeded.Id == metadata.Id ? seeded
                : new LocalizedMetadataText(metadata.Id, metadata.Title, metadata.Author, metadata.Keywords, metadata.Description),
            (master.Sections ?? Enumerable.Empty<ContentSection>())
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .Select(s => new LocalizedSectionText(s.Id, (s.Elements ?? Enumerable.Empty<SectionElement>())
                    .OrderBy(e => e.Id)
                    .Select(e => elements.GetValueOrDefault((s.Id, e.Id)) ?? new LocalizedElementText(e.Id, e.TinyText, e.EditorText))
                    .ToList()))
                .ToList());
    }

    // expectedSourceFingerprint: the editor's SourceFingerprint as of when it loaded; text: the
    // edited text keyed by master IDs, only the fields each element type edits.
    public async Task<ManualTranslationSaveResult> Save(int contentId, int cultureId, int applicationId, string expectedSourceFingerprint,
        LocalizedContentText text, CancellationToken cancellationToken = default)
    {
        if (!await _translations.ContentBelongsToApplication(contentId, applicationId, cancellationToken))
            return ManualTranslationSaveResult.NotFound;
        if (cultureId == 0 || await _translations.FindCulture(cultureId, cancellationToken) == null)
            return ManualTranslationSaveResult.CultureUnavailable;

        var master = await _translations.FindSourceGraph(contentId, cancellationToken);
        if (master == null)
            return ManualTranslationSaveResult.NotFound;
        // Text-only source edits keep the tree valid, so only the fingerprint catches them.
        var fingerprint = ContentSourceFingerprint.Compute(master);
        if (!string.Equals(fingerprint, expectedSourceFingerprint, StringComparison.Ordinal))
            return ManualTranslationSaveResult.SourceChanged;
        if (TranslationSourceDocument.ToLocalizedTextJson(master, text) is not { } localizedTextJson || !EditsOnlyTextFields(master, text))
            return ManualTranslationSaveResult.InvalidStructure;

        var translation = await _translations.FindTranslation(contentId, cultureId, cancellationToken);
        if (translation is { IsDeleted: true })
            return ManualTranslationSaveResult.TranslationDeleted;
        if (translation == null)
        {
            translation = new ContentTranslation { ContentId = contentId, CultureId = cultureId, IsActive = true };
            _translations.AddTranslation(translation);
        }

        translation.TranslationStatus = TranslationStatus.Ready;
        translation.SourceFingerprint = fingerprint;
        translation.LocalizedTextJson = localizedTextJson;
        translation.Provider = Provider;
        translation.Model = null;
        translation.TranslatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        translation.Error = null;

        // A source edit committed after the master read leaves this row's fingerprint behind, so
        // it reads as Stale - never as Ready for the newer source.
        return await _translations.TrySaveChanges(cancellationToken) ? ManualTranslationSaveResult.Saved : ManualTranslationSaveResult.Conflict;
    }

    // Expects text already matched to master's tree: each element may only carry text in the
    // field its master type edits.
    private static bool EditsOnlyTextFields(Content master, LocalizedContentText text)
    {
        var types = (master.Sections ?? Enumerable.Empty<ContentSection>()).SelectMany(s => s.Elements ?? Enumerable.Empty<SectionElement>())
            .ToDictionary(e => e.Id, e => e.ElementType);
        return (text.Sections ?? new List<LocalizedSectionText>()).SelectMany(s => s.Elements).All(e =>
            (e.TinyText == null || TinyTextTypes.Contains(types[e.Id])) && (e.EditorText == null || EditorTextTypes.Contains(types[e.Id])));
    }
}
