namespace Web.Areas.BackOffice.Features.Content;

// Pure ElementType -> SectionElementDto field routing extracted out of ContentController's
// SaveSection action. Update and Create intentionally use different EditorText sanitization rules
// per ElementType (1002 vs 1005) - this mirrors the original inline switches exactly, including
// that asymmetry, rather than "fixing" it.
internal static class SectionElementRequestMapper
{
    public static SectionElementDto BuildForUpdate(ContentBodyElementDto item)
    {
        return item.ElementType switch
        {
            1000 or 1006 or 1007 or 1008 or 1009 or 1010 or 1011 =>
                new SectionElementDto { Id = item.Id, TinyText = item.Value, ElementTitle = item.Title },
            1001 or 1004 =>
                new SectionElementDto { Id = item.Id, FileNameText = item.Value, ElementTitle = item.Title },
            1002 =>
                new SectionElementDto
                {
                    Id = item.Id,
                    EditorText = item.Value.Replace("<p></p>", "").Replace("<p> </p>", "").Replace("\n", ""),
                    ElementTitle = item.Title
                },
            1003 =>
                new SectionElementDto { Id = item.Id, GalleryImages = item.Value, ElementTitle = item.Title },
            1005 =>
                new SectionElementDto { Id = item.Id, EditorText = item.Value, ElementTitle = item.Title },
            _ => null
        };
    }

    public static SectionElementDto BuildForCreate(ContentBodyElementDto item, int sectionId)
    {
        return item.ElementType switch
        {
            1000 or 1006 or 1007 or 1008 or 1009 or 1010 or 1011 =>
                new SectionElementDto { TinyText = item.Value, SectionId = sectionId, IsActive = true, ElementType = item.ElementType, Size = item.Size, ElementTitle = item.Title },
            1001 or 1004 =>
                new SectionElementDto { FileNameText = item.Value, SectionId = sectionId, IsActive = true, ElementType = item.ElementType, Size = item.Size, ElementTitle = item.Title },
            1002 =>
                new SectionElementDto { EditorText = item.Value, SectionId = sectionId, IsActive = true, ElementType = item.ElementType, Size = item.Size, ElementTitle = item.Title },
            1003 =>
                new SectionElementDto { GalleryImages = item.Value, SectionId = sectionId, IsActive = true, ElementType = item.ElementType, Size = item.Size, ElementTitle = item.Title },
            1005 =>
                new SectionElementDto
                {
                    EditorText = item.Value.Replace("<p></p>", "").Replace("<p> </p>", "").Replace("\n", ""),
                    SectionId = sectionId,
                    IsActive = true,
                    ElementType = item.ElementType,
                    Size = item.Size,
                    ElementTitle = item.Title
                },
            _ => null
        };
    }
}
