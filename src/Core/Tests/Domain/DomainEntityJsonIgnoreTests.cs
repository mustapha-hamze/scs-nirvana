using System.Text.Json;
using Domains.Entities.ContentManagement;
using Xunit;

namespace Core.Tests.Domain;

// The app's actual JSON serializer is System.Text.Json (Program.cs calls AddMvc() with no
// AddNewtonsoftJson()). ContentImage/ContentSection/SectionElement used to mark their EF
// back-references with Newtonsoft.Json's [JsonIgnore], which STJ silently ignores — so those
// attributes never actually suppressed anything for a real response. They now use
// System.Text.Json.Serialization.JsonIgnore, matching ContentMetadata's existing (correct)
// pattern. These tests lock that in.
public class DomainEntityJsonIgnoreTests
{
    [Fact]
    public void ContentImage_Content_BackReferenceIsIgnored()
    {
        var content = new Content { Id = 1, Title = "Parent" };
        var image = new ContentImage { Id = 2, ContentId = 1, Content = content };

        var json = JsonSerializer.Serialize(image);

        Assert.DoesNotContain("\"Content\"", json);
        Assert.Contains("\"ContentId\":1", json);
    }

    [Fact]
    public void ContentSection_Content_BackReferenceIsIgnored()
    {
        var content = new Content { Id = 1, Title = "Parent" };
        var section = new ContentSection { Id = 3, ContentId = 1, Content = content };

        var json = JsonSerializer.Serialize(section);

        Assert.DoesNotContain("\"Content\"", json);
    }

    [Fact]
    public void SectionElement_Section_BackReferenceIsIgnored()
    {
        var section = new ContentSection { Id = 3, ContentId = 1 };
        var element = new SectionElement { Id = 4, SectionId = 3, Section = section };

        var json = JsonSerializer.Serialize(element);

        Assert.DoesNotContain("\"Section\"", json);
    }

    [Fact]
    public void FullContentGraph_WithBackReferences_SerializesWithoutCycleException()
    {
        // The same cycle shape that used to throw JsonException("A possible object cycle was
        // detected") for GetContent/{id} before the API DTO work — now guarded at the entity
        // level too, since the back-references are ignored regardless of who serializes them.
        var content = new Content { Id = 1, Title = "Parent" };
        var section = new ContentSection { Id = 10, ContentId = 1, Content = content };
        content.Sections = new List<ContentSection> { section };
        var element = new SectionElement { Id = 100, SectionId = 10, Section = section };
        section.Elements = new List<SectionElement> { element };
        var image = new ContentImage { Id = 200, ContentId = 1, Content = content };
        content.Images = new List<ContentImage> { image };

        var json = JsonSerializer.Serialize(content);

        Assert.NotEmpty(json);
    }
}
