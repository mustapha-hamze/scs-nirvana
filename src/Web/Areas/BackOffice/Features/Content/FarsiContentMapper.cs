using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using ContentEntity = Domains.Entities.ContentManagement.Content;

namespace Web.Areas.BackOffice.Features.Content;

// Pure Farsi content JSON parsing/mapping extracted out of ContentController: no data access, no
// side effects, just Content <-> FarsiContentEditDto transformation and the FarsiContent JSON
// round-trip the controller stores/reads via IContentServices/IContentProvider.
internal static class FarsiContentMapper
{
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Include
    };

    // Returns the Content graph to use as the editable Farsi source: always the current English
    // master's layout (sections, elements, images, files, titles), with stored Farsi text overlaid
    // onto nodes whose stable IDs still match. Text comes from the canonical translation when
    // usable, else the legacy FarsiContent snapshot; added nodes keep English, removed nodes are
    // gone. Unusable stored Farsi falls back to plain English (UsedEnglishFallback). Pure: never
    // writes anything. Always a detached clone (never `englishContent` itself) so Farsi edits can't
    // leak into the EF-tracked English entity or collide with its tracking identity.
    public static (ContentEntity Source, bool UsedEnglishFallback) GetEditSource(ContentEntity englishContent, LocalizedContentText canonicalText = null)
    {
        var source = CloneDetached(englishContent);
        source.FarsiContent = null; // never nest the old snapshot inside the new one

        var text = canonicalText ?? ReadLegacyText(englishContent);
        if (text == null)
            return (source, true);

        Overlay(source, text);
        return (source, false);
    }

    private static LocalizedContentText ReadLegacyText(ContentEntity englishContent)
    {
        if (string.IsNullOrWhiteSpace(englishContent.FarsiContent))
            return null;

        ContentEntity legacy;
        try
        {
            legacy = JsonConvert.DeserializeObject<ContentEntity>(englishContent.FarsiContent, JsonSettings);
        }
        catch (JsonException)
        {
            return null;
        }

        // A snapshot of some other content (or without an Id) is unusable, not partially trusted.
        if (legacy == null || legacy.Id != englishContent.Id)
            return null;

        var metadata = legacy.Metadata;
        return new LocalizedContentText(legacy.Title, legacy.HeadLine, legacy.Abstract, legacy.Description,
            metadata == null ? null : new LocalizedMetadataText(metadata.Id, metadata.Title, metadata.Author, metadata.Keywords, metadata.Description),
            (legacy.Sections ?? new List<ContentSection>()).Where(s => s != null)
                .Select(s => new LocalizedSectionText(s.Id, (s.Elements ?? new List<SectionElement>()).Where(e => e != null)
                    .Select(e => new LocalizedElementText(e.Id, e.TinyText, e.EditorText)).ToList()))
                .ToList());
    }

    private static void Overlay(ContentEntity source, LocalizedContentText text)
    {
        source.Title = text.Title;
        source.HeadLine = text.HeadLine;
        source.Abstract = text.Abstract;
        source.Description = text.Description;

        if (source.Metadata != null && text.Metadata != null && text.Metadata.Id == source.Metadata.Id)
        {
            source.Metadata.Title = text.Metadata.Title;
            source.Metadata.Author = text.Metadata.Author;
            source.Metadata.Keywords = text.Metadata.Keywords;
            source.Metadata.Description = text.Metadata.Description;
        }

        // Keyed by (section, element): text only lands on an element still in the same section.
        // Duplicate IDs in a historical payload: the first wins.
        var elementTexts = new Dictionary<(int, int), LocalizedElementText>();
        foreach (var section in (text.Sections ?? new List<LocalizedSectionText>()).Where(s => s?.Elements != null))
            foreach (var element in section.Elements.Where(e => e != null))
                elementTexts.TryAdd((section.Id, element.Id), element);

        foreach (var section in source.Sections ?? new List<ContentSection>())
        {
            foreach (var element in section.Elements ?? new List<SectionElement>())
            {
                if (!elementTexts.TryGetValue((section.Id, element.Id), out var translated))
                    continue;
                element.TinyText = translated.TinyText;
                element.EditorText = translated.EditorText;
            }
        }
    }

    public static ContentEntity CloneDetached(ContentEntity content)
    {
        return JsonConvert.DeserializeObject<ContentEntity>(JsonConvert.SerializeObject(content, JsonSettings), JsonSettings);
    }

    public static FarsiContentEditDto ToEditDto(ContentEntity content)
    {
        return new FarsiContentEditDto
        {
            Id = content.Id,
            ApplicationId = content.ApplicationId,
            TypeId = content.TypeId,
            Title = content.Title,
            HeadLine = content.HeadLine,
            Abstract = content.Abstract,
            Description = content.Description,
            PublishDt = content.PublishDt,
            Metadata = new FarsiContentMetadataEditDto
            {
                Id = content.Metadata?.Id ?? 0,
                ContentId = content.Id,
                Title = content.Metadata?.Title,
                Author = content.Metadata?.Author,
                Keywords = content.Metadata?.Keywords,
                Description = content.Metadata?.Description
            },
            Sections = (content.Sections ?? new List<ContentSection>())
                .OrderBy(s => s.Priority)
                .Select(s => new FarsiSectionEditDto
                {
                    Id = s.Id,
                    ContentId = s.ContentId,
                    Priority = s.Priority,
                    SectionElements = (s.Elements ?? new List<SectionElement>())
                        .Select(e => new FarsiSectionElementEditDto
                        {
                            Id = e.Id,
                            SectionId = e.SectionId,
                            ElementType = e.ElementType,
                            TinyText = e.TinyText,
                            EditorText = e.EditorText,
                            FileNameText = e.FileNameText,
                            GalleryImages = e.GalleryImages,
                            ElementTitle = e.ElementTitle,
                            Size = e.Size
                        }).ToList()
                }).ToList()
        };
    }

    // Applies the edited Farsi fields onto `baseContent` in place (the previously saved Farsi
    // graph, or a fresh English clone). Only known section elements/types are touched, exactly as
    // the original inline controller logic did.
    public static void ApplyEdit(ContentEntity baseContent, FarsiContentEditDto model)
    {
        baseContent.Title = model.Title;
        baseContent.HeadLine = model.HeadLine;
        baseContent.Abstract = model.Abstract;
        baseContent.Description = model.Description;

        if (model.Metadata != null)
        {
            if (baseContent.Metadata == null)
                baseContent.Metadata = new ContentMetadata { ContentId = baseContent.Id };

            baseContent.Metadata.Title = model.Metadata.Title;
            baseContent.Metadata.Author = model.Metadata.Author;
            baseContent.Metadata.Keywords = model.Metadata.Keywords;
            baseContent.Metadata.Description = model.Metadata.Description;
        }

        if (model.Sections == null || baseContent.Sections == null)
            return;

        foreach (var sectionEdit in model.Sections)
        {
            var section = baseContent.Sections.FirstOrDefault(s => s.Id == sectionEdit.Id);
            if (section?.Elements == null || sectionEdit.SectionElements == null)
                continue;

            foreach (var elementEdit in sectionEdit.SectionElements)
            {
                var element = section.Elements.FirstOrDefault(e => e.Id == elementEdit.Id);
                if (element == null)
                    continue;

                switch (element.ElementType)
                {
                    case 1000:
                    case 1006:
                    case 1007:
                    case 1008:
                    case 1009:
                    case 1010:
                    case 1011:
                        element.TinyText = elementEdit.TinyText;
                        break;
                    case 1002:
                    case 1005:
                        element.EditorText = elementEdit.EditorText;
                        break;
                }
            }
        }
    }

    public static string SerializeForStorage(ContentEntity content)
    {
        return JsonConvert.SerializeObject(content, JsonSettings);
    }
}
