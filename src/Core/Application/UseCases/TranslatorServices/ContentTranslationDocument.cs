using System;
using System.Collections.Generic;
using System.Linq;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// Explicit whitelist of the Content fields the translation prompt reads or protects, assembled
// at the Application boundary instead of serializing the raw EF-navigable Content entity graph.
// The entity graph needed ReferenceLoopHandling.Ignore to work around back-reference cycles
// (Content.Sections[].Elements[].Section, etc.) that Newtonsoft doesn't respect
// [System.Text.Json.Serialization.JsonIgnore] on; this document has no back-references, so no
// cycle-handling workaround is needed. Property names must match OpenAiTranslationPort's prompt
// and TranslationOutputValidator's field lists exactly - FarsiContent is deliberately excluded
// (it's the translation's output target, always null on input, and not part of either list).
public class ContentTranslationDocument
{
    public int Id { get; init; }
    public int Status { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsActive { get; init; }
    public DateTime UpdatedDT { get; init; }
    public DateTime CreatedDT { get; init; }
    public int ApplicationId { get; init; }
    public int TypeId { get; init; }
    public string Title { get; init; }
    public string HeadLine { get; init; }
    public string Abstract { get; init; }
    public string Description { get; init; }
    public string Categories { get; init; }
    public string Tags { get; init; }
    public string Cultures { get; init; }
    public DateTime PublishDt { get; init; }
    public List<ContentSectionTranslationDocument> Sections { get; init; }
    public List<ContentImageTranslationDocument> Images { get; init; }
    public ContentMetadataTranslationDocument Metadata { get; init; }

    public static ContentTranslationDocument FromContent(Content content) => new()
    {
        Id = content.Id,
        Status = content.Status,
        IsDeleted = content.IsDeleted,
        IsActive = content.IsActive,
        UpdatedDT = content.UpdatedDT,
        CreatedDT = content.CreatedDT,
        ApplicationId = content.ApplicationId,
        TypeId = content.TypeId,
        Title = content.Title,
        HeadLine = content.HeadLine,
        Abstract = content.Abstract,
        Description = content.Description,
        Categories = content.Categories,
        Tags = content.Tags,
        Cultures = content.Cultures,
        PublishDt = content.PublishDt,
        Sections = content.Sections?.Select(ContentSectionTranslationDocument.FromEntity).ToList(),
        Images = content.Images?.Select(ContentImageTranslationDocument.FromEntity).ToList(),
        Metadata = content.Metadata == null ? null : ContentMetadataTranslationDocument.FromEntity(content.Metadata)
    };
}

public class ContentSectionTranslationDocument
{
    public int Id { get; init; }
    public int Status { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsActive { get; init; }
    public DateTime UpdatedDT { get; init; }
    public DateTime CreatedDT { get; init; }
    public int ContentId { get; init; }
    public int Priority { get; init; }
    public List<SectionElementTranslationDocument> Elements { get; init; }

    public static ContentSectionTranslationDocument FromEntity(ContentSection section) => new()
    {
        Id = section.Id,
        Status = section.Status,
        IsDeleted = section.IsDeleted,
        IsActive = section.IsActive,
        UpdatedDT = section.UpdatedDT,
        CreatedDT = section.CreatedDT,
        ContentId = section.ContentId,
        Priority = section.Priority,
        Elements = section.Elements?.Select(SectionElementTranslationDocument.FromEntity).ToList()
    };
}

public class SectionElementTranslationDocument
{
    public int Id { get; init; }
    public int Status { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsActive { get; init; }
    public DateTime UpdatedDT { get; init; }
    public DateTime CreatedDT { get; init; }
    public int SectionId { get; init; }
    public int ElementType { get; init; }
    public string TinyText { get; init; }
    public string EditorText { get; init; }
    public string FileNameText { get; init; }
    public string GalleryImages { get; init; }
    public int Size { get; init; }
    public string ElementTitle { get; init; }

    public static SectionElementTranslationDocument FromEntity(SectionElement element) => new()
    {
        Id = element.Id,
        Status = element.Status,
        IsDeleted = element.IsDeleted,
        IsActive = element.IsActive,
        UpdatedDT = element.UpdatedDT,
        CreatedDT = element.CreatedDT,
        SectionId = element.SectionId,
        ElementType = element.ElementType,
        TinyText = element.TinyText,
        EditorText = element.EditorText,
        FileNameText = element.FileNameText,
        GalleryImages = element.GalleryImages,
        Size = element.Size,
        ElementTitle = element.ElementTitle
    };
}

public class ContentImageTranslationDocument
{
    public int Id { get; init; }
    public int Status { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsActive { get; init; }
    public DateTime UpdatedDT { get; init; }
    public DateTime CreatedDT { get; init; }
    public int ContentId { get; init; }
    public string ImageFileName { get; init; }
    public int Size { get; init; }

    public static ContentImageTranslationDocument FromEntity(ContentImage image) => new()
    {
        Id = image.Id,
        Status = image.Status,
        IsDeleted = image.IsDeleted,
        IsActive = image.IsActive,
        UpdatedDT = image.UpdatedDT,
        CreatedDT = image.CreatedDT,
        ContentId = image.ContentId,
        ImageFileName = image.ImageFileName,
        Size = image.Size
    };
}

public class ContentMetadataTranslationDocument
{
    public int Id { get; init; }
    public int Status { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsActive { get; init; }
    public DateTime UpdatedDT { get; init; }
    public DateTime CreatedDT { get; init; }
    public int ContentId { get; init; }
    public string Title { get; init; }
    public string Author { get; init; }
    public string Keywords { get; init; }
    public string Description { get; init; }

    public static ContentMetadataTranslationDocument FromEntity(ContentMetadata metadata) => new()
    {
        Id = metadata.Id,
        Status = metadata.Status,
        IsDeleted = metadata.IsDeleted,
        IsActive = metadata.IsActive,
        UpdatedDT = metadata.UpdatedDT,
        CreatedDT = metadata.CreatedDT,
        ContentId = metadata.ContentId,
        Title = metadata.Title,
        Author = metadata.Author,
        Keywords = metadata.Keywords,
        Description = metadata.Description
    };
}
