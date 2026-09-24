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
    Conflict
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

    // translatedGraph: the edited translation as a Content graph; farsiContentJson: its legacy
    // FarsiContent serialization, stored unchanged for the deferred read cutover.
    public async Task<ManualTranslationSaveResult> Save(int contentId, int cultureId, int applicationId, Content translatedGraph,
        string farsiContentJson, CancellationToken cancellationToken = default)
    {
        if (!await _translations.ContentBelongsToApplication(contentId, applicationId, cancellationToken))
            return ManualTranslationSaveResult.NotFound;
        if (cultureId == 0 || await _translations.FindCulture(cultureId, cancellationToken) == null)
            return ManualTranslationSaveResult.CultureUnavailable;

        var master = await _translations.FindSourceGraph(contentId, cancellationToken);
        if (master == null)
            return ManualTranslationSaveResult.NotFound;
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
        translation.SourceFingerprint = ContentSourceFingerprint.Compute(master);
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
