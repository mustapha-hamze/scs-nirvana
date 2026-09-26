using System;
using System.Threading.Tasks;
using Application.CMSRepository;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

public enum ManualTranslationSaveResult
{
    Saved,
    NotFound,
    CultureUnavailable,

    // The edited graph doesn't match the current master (stale or tampered IDs/tree, or HTML).
    InvalidStructure,

    // The (content, culture) translation row is soft-deleted; it's never resurrected.
    TranslationDeleted,

    // A concurrent writer won; nothing was saved.
    Conflict,

    // The English source changed since the editor loaded (fingerprint mismatch); nothing was
    // saved - the editor must reload and review against the current source.
    SourceChanged
}

// Manual (editor) translation save: keeps the legacy FarsiContent snapshot and the canonical
// ContentTranslation for the activation culture in step. Validates before writing anything, and
// both writes commit in one SaveChanges or not at all. Never calls the translation provider.
public class ManualContentTranslation
{
    public const string Provider = "manual";

    private readonly IContentTranslationJobRepository _translations;
    private readonly IContentCommandRepository _contentCommands;
    private readonly TimeProvider _timeProvider;

    public ManualContentTranslation(IContentTranslationJobRepository translations, IContentCommandRepository contentCommands, TimeProvider timeProvider)
    {
        _translations = translations;
        _contentCommands = contentCommands;
        _timeProvider = timeProvider;
    }

    // Read-only: the current source fingerprint the editor must send back on save, or null when
    // the content isn't the application's.
    public async Task<string> GetSourceFingerprint(int contentId, int applicationId, CancellationToken cancellationToken = default)
    {
        if (!await _translations.ContentBelongsToApplication(contentId, applicationId, cancellationToken))
            return null;
        return await _translations.FindSourceGraph(contentId, cancellationToken) is { } master ? ContentSourceFingerprint.Compute(master) : null;
    }

    // Read-only: the canonical translation text the editor should start from, or null when the
    // content isn't the application's or there's no usable (non-deleted, structurally valid)
    // payload. Any status qualifies - it only seeds the form; saving re-validates and sets Ready.
    public async Task<LocalizedContentText> GetStoredText(int contentId, int cultureId, int applicationId, CancellationToken cancellationToken = default)
    {
        if (cultureId == 0 || !await _translations.ContentBelongsToApplication(contentId, applicationId, cancellationToken))
            return null;

        var translation = await _translations.FindTranslation(contentId, cultureId, cancellationToken);
        return translation is { IsDeleted: false } ? LegacyFarsiContentParser.ReadStored(translation.LocalizedTextJson) : null;
    }

    // expectedSourceFingerprint: GetSourceFingerprint as of when the editor loaded; translatedGraph:
    // the edited translation as a Content graph; farsiContentJson: its legacy FarsiContent
    // serialization, stored unchanged for the deferred read cutover.
    public async Task<ManualTranslationSaveResult> Save(int contentId, int cultureId, int applicationId, string expectedSourceFingerprint,
        Content translatedGraph, string farsiContentJson, CancellationToken cancellationToken = default)
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
        if (translatedGraph?.Id != contentId || TranslationSourceDocument.ToLocalizedTextJson(master, translatedGraph) is not { } localizedTextJson)
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

        await _contentCommands.UpdateFarsiContent(contentId, farsiContentJson, cancellationToken);

        // A source edit committed after the master read leaves this row's fingerprint behind, so
        // it reads as Stale - never as Ready for the newer source.
        return await _translations.TrySaveChanges(cancellationToken) ? ManualTranslationSaveResult.Saved : ManualTranslationSaveResult.Conflict;
    }
}
