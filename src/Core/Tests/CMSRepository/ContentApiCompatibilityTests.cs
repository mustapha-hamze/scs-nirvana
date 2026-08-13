using System.Text.Json;
using Application.Contracts.CMSApi;
using Xunit;

namespace Core.Tests.CMSRepository;

// Compatibility proof for the public content API DTOs (ContentApiDto, ContentImageApiDto,
// ContentSectionApiDto, SectionElementApiDto, ContentMetadataApiDto, BlogIndexApiDto), which
// replaced raw Content/BlogIndexDto entity responses. These tests operate purely on the DTOs and
// System.Text.Json — the app's actual, unconfigured serializer (Program.cs calls AddMvc() with
// no AddNewtonsoftJson()) — so they need no database. They exist to make explicit exactly what
// the earlier ContentApiReadTests assert incidentally: the full set of JSON keys a client sees,
// including which ones are now intentionally absent.
public class ContentApiCompatibilityTests
{
    private static readonly JsonSerializerOptions ProdLikeJsonOptions = new();

    [Fact]
    public void ContentApiDto_FullyPopulatedGraph_ProducesExpectedTopLevelKeys()
    {
        var dto = new ContentApiDto
        {
            Id = 1,
            Status = 1,
            IsDeleted = false,
            IsActive = true,
            UpdatedDT = DateTime.Now,
            CreatedDT = DateTime.Now,
            ApplicationId = 1,
            TypeId = 1000,
            Title = "Title",
            HeadLine = "HeadLine",
            Abstract = "Abstract",
            Description = "Description",
            FarsiContent = "{}",
            Categories = "1|2",
            Tags = "3|4",
            Cultures = "5",
            PublishDt = DateTime.Now,
            Images = new List<ContentImageApiDto>(),
            Sections = new List<ContentSectionApiDto>(),
            Metadata = new ContentMetadataApiDto()
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, ProdLikeJsonOptions));
        var propertyNames = document.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        var expected = new[]
        {
            "Id", "Status", "IsDeleted", "IsActive", "UpdatedDT", "CreatedDT",
            "ApplicationId", "TypeId", "Title", "HeadLine", "Abstract", "Description",
            "FarsiContent", "Categories", "Tags", "Cultures", "PublishDt",
            "Sections", "Images", "Metadata"
        };
        Assert.Equal(expected.ToHashSet(), propertyNames);

        // The one deliberate exception to "mirror the entity exactly": Content.Application is
        // never populated by any of the six public read methods (none of them .Include() it), so
        // a client could never have observed anything but null for this field. Dropping it from
        // the DTO removes a key that was always present-but-null, not one that ever carried data.
        Assert.False(document.RootElement.TryGetProperty("Application", out _));
    }

    [Fact]
    public void ContentSectionApiDto_WithElements_NestsCorrectly()
    {
        var dto = new ContentSectionApiDto
        {
            Id = 10,
            ContentId = 1,
            Priority = 1,
            Elements = new List<SectionElementApiDto>
            {
                new() { Id = 100, SectionId = 10, ElementType = 1000, TinyText = "hi" }
            }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, ProdLikeJsonOptions));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("Priority").GetInt32());
        var elements = root.GetProperty("Elements");
        Assert.Equal(1, elements.GetArrayLength());
        var element = elements[0];
        Assert.Equal(10, element.GetProperty("SectionId").GetInt32());
        Assert.Equal("hi", element.GetProperty("TinyText").GetString());
        Assert.Equal(1000, element.GetProperty("ElementType").GetInt32());
    }

    [Fact]
    public void ContentSectionApiDto_WithoutElements_SerializesElementsAsNull()
    {
        // Matches the no-page GetContentByTypeId endpoint, which includes sections but never
        // ThenIncludes their elements — Elements must serialize as null, not an empty array, so
        // a client distinguishing "no elements loaded" from "section genuinely has zero elements"
        // keeps working.
        var dto = new ContentSectionApiDto { Id = 10, ContentId = 1, Priority = 1, Elements = null };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, ProdLikeJsonOptions));

        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("Elements").ValueKind);
    }

    [Fact]
    public void ContentApiDto_WithoutMetadata_SerializesMetadataAsNull()
    {
        var dto = new ContentApiDto { Id = 1, Title = "Title", Metadata = null };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, ProdLikeJsonOptions));

        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("Metadata").ValueKind);
    }

    [Fact]
    public void ContentMetadataApiDto_SerializesScalarFields()
    {
        var dto = new ContentMetadataApiDto { Id = 1, ContentId = 1, Title = "Meta title", Author = "Author", Keywords = "kw", Description = "desc" };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, ProdLikeJsonOptions));
        var root = document.RootElement;

        Assert.Equal("Meta title", root.GetProperty("Title").GetString());
        Assert.Equal("Author", root.GetProperty("Author").GetString());
        Assert.Equal("kw", root.GetProperty("Keywords").GetString());
        Assert.Equal("desc", root.GetProperty("Description").GetString());
        Assert.Equal(1, root.GetProperty("ContentId").GetInt32());
    }

    [Fact]
    public void ContentImageApiDto_SerializesScalarFields()
    {
        var dto = new ContentImageApiDto { Id = 1, ContentId = 5, ImageFileName = "hero.jpg", Size = 640 };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, ProdLikeJsonOptions));
        var root = document.RootElement;

        Assert.Equal(5, root.GetProperty("ContentId").GetInt32());
        Assert.Equal("hero.jpg", root.GetProperty("ImageFileName").GetString());
        Assert.Equal(640, root.GetProperty("Size").GetInt32());
    }

    [Fact]
    public void BlogIndexApiDto_FullShape_ProducesExpectedTopLevelKeysAndNestedContents()
    {
        var dto = new BlogIndexApiDto
        {
            Contents = new List<ContentApiDto>
            {
                new() { Id = 1, Title = "Item", Categories = "1", Images = new List<ContentImageApiDto> { new() { Id = 2, ContentId = 1, ImageFileName = "a.jpg", Size = 640 } } }
            },
            PagesCount = 2,
            PageIndex = 1
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, ProdLikeJsonOptions));
        var root = document.RootElement;
        var propertyNames = root.EnumerateObject().Select(p => p.Name).ToHashSet();

        Assert.Equal(new[] { "Contents", "PagesCount", "PageIndex" }.ToHashSet(), propertyNames);
        Assert.Equal(2, root.GetProperty("PagesCount").GetInt32());
        Assert.Equal(1, root.GetProperty("PageIndex").GetInt32());

        var content = root.GetProperty("Contents")[0];
        Assert.Equal("Item", content.GetProperty("Title").GetString());
        Assert.Equal(1, content.GetProperty("Images").GetArrayLength());
    }

    [Fact]
    public void BlogIndexApiDto_EmptyContents_SerializesAsEmptyArrayNotNull()
    {
        var dto = new BlogIndexApiDto { Contents = new List<ContentApiDto>(), PagesCount = 0, PageIndex = 0 };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, ProdLikeJsonOptions));
        var contents = document.RootElement.GetProperty("Contents");

        Assert.Equal(JsonValueKind.Array, contents.ValueKind);
        Assert.Equal(0, contents.GetArrayLength());
    }
}
