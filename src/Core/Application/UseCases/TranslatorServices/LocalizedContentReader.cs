using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Application.CMSRepository;
using Application.Contracts.CMSApi;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

public enum LocalizedContentReadStatus
{
    Found,

    // Rollout gate closed for the environment or the application.
    Disabled,
    NotFound,
    InvalidCulture
}

// Content is set only when Status is Found.
public record LocalizedContentReadResult(LocalizedContentReadStatus Status, LocalizedContentApiDto Content);

// Read-only resolution of the current master content for one culture: a Ready translation of the
// current source, else (legacy Farsi culture only) the legacy FarsiContent rebased onto the
// current master IDs, else the English master. Anything stale, deleted, for another culture or
// structurally off is skipped, never served. Never writes, queues jobs or calls the provider.
public partial class LocalizedContentReader
{
    private readonly ILocalizedContentReadRepository _repository;
    private readonly ContentTranslationOptions _options;

    public LocalizedContentReader(ILocalizedContentReadRepository repository, ContentTranslationOptions options)
    {
        _repository = repository;
        _options = options;
    }

    // BCP 47-style language tag, e.g. "fa", "fa-IR", "zh-Hant-TW".
    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$")]
    private static partial Regex CultureCode();

    public async Task<LocalizedContentReadResult> Read(int contentId, int applicationId, string culture, CancellationToken cancellationToken = default)
    {
        if (!_options.LocalizedReadEnabled || _options.LocalizedReadApplicationIds?.Contains(applicationId) != true)
            return new LocalizedContentReadResult(LocalizedContentReadStatus.Disabled, null);
        if (culture == null || !CultureCode().IsMatch(culture))
            return new LocalizedContentReadResult(LocalizedContentReadStatus.InvalidCulture, null);

        var master = await _repository.FindMasterGraph(contentId, applicationId, cancellationToken);
        if (master == null)
            return new LocalizedContentReadResult(LocalizedContentReadStatus.NotFound, null);

        var resolvedCulture = await _repository.FindActiveCulture(applicationId, culture, cancellationToken);
        var (text, resolution) = resolvedCulture == null
            ? (null, LocalizedContentResolution.Source)
            : await Resolve(master, resolvedCulture.Id, cancellationToken);

        var content = Project(master, text);
        content.Culture = resolvedCulture?.Key;
        content.Resolution = resolution;
        return new LocalizedContentReadResult(LocalizedContentReadStatus.Found, content);
    }

    private async Task<(LocalizedContentText Text, string Resolution)> Resolve(Content master, int cultureId, CancellationToken cancellationToken)
    {
        var translation = await _repository.FindTranslation(master.Id, cultureId, cancellationToken);
        if (translation?.CultureId == cultureId
            && ContentTranslationJobProcessor.IsReady(translation, ContentSourceFingerprint.Compute(master))
            && LegacyFarsiContentParser.ReadStored(translation.LocalizedTextJson) is { } stored
            && MatchesMaster(stored, master))
            return (stored, LocalizedContentResolution.Translation);

        if (_options.LegacyFarsiCultureId != 0 && cultureId == _options.LegacyFarsiCultureId
            && !string.IsNullOrWhiteSpace(master.FarsiContent)
            && LegacyFarsiContentParser.Parse(master.FarsiContent, master) is { ReasonCode: null } legacy)
            return (LegacyFarsiContentParser.Deserialize(legacy.LocalizedTextJson), LocalizedContentResolution.LegacyFarsi);

        return (null, LocalizedContentResolution.Source);
    }

    // A matching fingerprint should imply this; checked anyway so a corrupt payload is never
    // half-applied. Exactly master's metadata, section and per-section element IDs.
    private static bool MatchesMaster(LocalizedContentText text, Content master)
    {
        if (text.Metadata?.Id != master.Metadata?.Id)
            return false;
        var masterSections = (master.Sections ?? Enumerable.Empty<ContentSection>()).ToList();
        if (text.Sections.Count != masterSections.Count)
            return false;

        var sections = text.Sections.ToDictionary(s => s.Id);
        return masterSections.All(s => sections.TryGetValue(s.Id, out var translated)
            && translated.Elements.Select(e => e.Id).ToHashSet().SetEquals((s.Elements ?? Enumerable.Empty<SectionElement>()).Select(e => e.Id)));
    }

    // Master layout with text overlaid where the IDs match; nodes the text lacks keep English.
    private static LocalizedContentApiDto Project(Content master, LocalizedContentText text)
    {
        var elementTexts = new Dictionary<(int, int), LocalizedElementText>();
        foreach (var section in text?.Sections ?? new List<LocalizedSectionText>())
            foreach (var element in section.Elements)
                elementTexts.TryAdd((section.Id, element.Id), element);
        var metadataText = text?.Metadata?.Id == master.Metadata?.Id ? text?.Metadata : null;

        return new LocalizedContentApiDto
        {
            Id = master.Id,
            ApplicationId = master.ApplicationId,
            TypeId = master.TypeId,
            Title = text == null ? master.Title : text.Title,
            HeadLine = text == null ? master.HeadLine : text.HeadLine,
            Abstract = text == null ? master.Abstract : text.Abstract,
            Description = text == null ? master.Description : text.Description,
            Categories = master.Categories,
            Tags = master.Tags,
            Cultures = master.Cultures,
            PublishDt = master.PublishDt,
            Metadata = master.Metadata == null ? null : new ContentMetadataApiDto
            {
                Id = master.Metadata.Id,
                Status = master.Metadata.Status,
                IsActive = master.Metadata.IsActive,
                UpdatedDT = master.Metadata.UpdatedDT,
                CreatedDT = master.Metadata.CreatedDT,
                ContentId = master.Metadata.ContentId,
                Title = metadataText == null ? master.Metadata.Title : metadataText.Title,
                Author = metadataText == null ? master.Metadata.Author : metadataText.Author,
                Keywords = metadataText == null ? master.Metadata.Keywords : metadataText.Keywords,
                Description = metadataText == null ? master.Metadata.Description : metadataText.Description
            },
            Images = (master.Images ?? Enumerable.Empty<ContentImage>()).OrderBy(i => i.Id).Select(i => new ContentImageApiDto
            {
                Id = i.Id,
                Status = i.Status,
                IsActive = i.IsActive,
                UpdatedDT = i.UpdatedDT,
                CreatedDT = i.CreatedDT,
                ContentId = i.ContentId,
                ImageFileName = i.ImageFileName,
                Size = i.Size
            }).ToList(),
            Sections = (master.Sections ?? Enumerable.Empty<ContentSection>()).OrderBy(s => s.Priority).ThenBy(s => s.Id).Select(s => new ContentSectionApiDto
            {
                Id = s.Id,
                Status = s.Status,
                IsActive = s.IsActive,
                UpdatedDT = s.UpdatedDT,
                CreatedDT = s.CreatedDT,
                ContentId = s.ContentId,
                Priority = s.Priority,
                Elements = (s.Elements ?? Enumerable.Empty<SectionElement>()).OrderBy(e => e.Id).Select(e =>
                {
                    var translated = elementTexts.GetValueOrDefault((s.Id, e.Id));
                    return new SectionElementApiDto
                    {
                        Id = e.Id,
                        Status = e.Status,
                        IsActive = e.IsActive,
                        UpdatedDT = e.UpdatedDT,
                        CreatedDT = e.CreatedDT,
                        SectionId = e.SectionId,
                        ElementType = e.ElementType,
                        TinyText = translated == null ? e.TinyText : translated.TinyText,
                        EditorText = translated == null ? e.EditorText : translated.EditorText,
                        FileNameText = e.FileNameText,
                        GalleryImages = e.GalleryImages,
                        Size = e.Size,
                        ElementTitle = e.ElementTitle
                    };
                }).ToList()
            }).ToList()
        };
    }
}
