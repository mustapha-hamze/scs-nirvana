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

    // Returns the Content graph to use as the editable Farsi source: the previously saved
    // FarsiContent JSON when present and valid, otherwise the English content as a starting point.
    // Always returns a detached clone (never `englishContent` itself) so mutating it for Farsi
    // edits can never leak into the EF-tracked English entity, and so it can't collide with
    // `englishContent`'s own tracking identity when later saved.
    public static (ContentEntity Source, bool UsedEnglishFallback) GetEditSource(ContentEntity englishContent)
    {
        if (string.IsNullOrWhiteSpace(englishContent.FarsiContent))
            return (CloneDetached(englishContent), true);

        try
        {
            var parsed = JsonConvert.DeserializeObject<ContentEntity>(englishContent.FarsiContent, JsonSettings);
            return parsed == null ? (CloneDetached(englishContent), true) : (parsed, false);
        }
        catch (JsonException)
        {
            return (CloneDetached(englishContent), true);
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
