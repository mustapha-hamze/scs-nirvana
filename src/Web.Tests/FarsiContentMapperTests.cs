using System.Collections.Generic;
using System.Linq;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Web.Areas.BackOffice.Features.Content;
using Xunit;

namespace Web.Tests;

// The Farsi editor's source graph is always the current master layout with stored Farsi text
// overlaid by stable ID.
public sealed class FarsiContentMapperTests
{
    // Master now: section 30 (element 40 kept, 41 added), section 31 added; metadata 20 kept.
    private static Content Master(string farsiContent = null) => new()
    {
        Id = 10, TypeId = 1000, Title = "EN title", HeadLine = "EN head", FarsiContent = farsiContent,
        Metadata = new ContentMetadata { Id = 20, ContentId = 10, Title = "EN meta", Author = "EN author" },
        Images = new List<ContentImage> { new() { Id = 50, ContentId = 10, ImageFileName = "img.jpg" } },
        Sections = new List<ContentSection>
        {
            new() { Id = 31, ContentId = 10, Priority = 1, Elements = new List<SectionElement>
                { new() { Id = 42, SectionId = 31, ElementType = 1000, TinyText = "EN 42", FileNameText = "f.pdf", ElementTitle = "title-42" } } },
            new() { Id = 30, ContentId = 10, Priority = 2, Elements = new List<SectionElement>
                {
                    new() { Id = 40, SectionId = 30, ElementType = 1000, TinyText = "EN 40", GalleryImages = "g.jpg" },
                    new() { Id = 41, SectionId = 30, ElementType = 1002, EditorText = "<p>EN 41</p>" }
                } }
        }
    };

    // Snapshot taken before the layout changed: section 30 had priority 1 and element 43 (since
    // removed); section 32 (since removed) existed.
    private const string StaleLegacy = """
        {"Id":10,"Title":"FA title","HeadLine":null,"FarsiContent":"nested",
         "Metadata":{"Id":20,"Title":"FA meta","Author":"FA author"},
         "Sections":[
           {"Id":30,"Priority":1,"Elements":[{"Id":40,"TinyText":"FA 40","FileNameText":"stale.pdf"},{"Id":43,"TinyText":"FA 43"}]},
           {"Id":32,"Priority":2,"Elements":[{"Id":44,"TinyText":"FA 44"}]}]}
        """;

    private static IEnumerable<SectionElement> Elements(Content c) => c.Sections.SelectMany(s => s.Elements);

    [Fact]
    public void StaleLegacySnapshot_IsRebasedOntoCurrentMaster()
    {
        var master = Master(StaleLegacy);

        var (source, usedEnglishFallback) = FarsiContentMapper.GetEditSource(master);

        Assert.False(usedEnglishFallback);
        Assert.NotSame(master, source);
        Assert.Null(source.FarsiContent);
        Assert.Equal(("FA title", null), (source.Title, source.HeadLine));
        Assert.Equal((20, "FA meta", "FA author"), (source.Metadata.Id, source.Metadata.Title, source.Metadata.Author));

        // Current layout, order and non-text fields come from master; removed nodes are gone.
        Assert.Equal(new[] { 31, 30 }, source.Sections.Select(s => s.Id));
        Assert.Equal(new[] { 42, 40, 41 }, Elements(source).Select(e => e.Id));
        Assert.Equal(new[] { 1, 2 }, source.Sections.Select(s => s.Priority));
        Assert.Equal("img.jpg", source.Images.Single().ImageFileName);

        var e40 = Elements(source).Single(e => e.Id == 40);
        Assert.Equal(("FA 40", "g.jpg", null), (e40.TinyText, e40.GalleryImages, e40.FileNameText));
        Assert.Equal("<p>EN 41</p>", Elements(source).Single(e => e.Id == 41).EditorText);
        var e42 = Elements(source).Single(e => e.Id == 42);
        Assert.Equal(("EN 42", "f.pdf", "title-42"), (e42.TinyText, e42.FileNameText, e42.ElementTitle));

        // The tracked master itself is never touched.
        Assert.Equal(("EN title", "EN 40"), (master.Title, Elements(master).Single(e => e.Id == 40).TinyText));
    }

    [Fact]
    public void CanonicalText_IsPreferredOverLegacy()
    {
        var canonical = new LocalizedContentText("CANON title", null, null, null, new LocalizedMetadataText(20, "CANON meta", null, null, null),
            new List<LocalizedSectionText> { new(30, new List<LocalizedElementText> { new(40, "CANON 40", null), new(43, "CANON 43", null) }) });

        var (source, usedEnglishFallback) = FarsiContentMapper.GetEditSource(Master(StaleLegacy), canonical);

        Assert.False(usedEnglishFallback);
        Assert.Equal(("CANON title", "CANON meta"), (source.Title, source.Metadata.Title));
        Assert.Equal("CANON 40", Elements(source).Single(e => e.Id == 40).TinyText);
        Assert.Equal("EN 42", Elements(source).Single(e => e.Id == 42).TinyText);
    }

    [Fact]
    public void TextOnlyLandsOnMatchingNodes_MetadataAndMovedElementsKeepEnglish()
    {
        const string legacy = """{"Id":10,"Title":"FA","Metadata":{"Id":21,"Title":"FA meta"},"Sections":[{"Id":31,"Elements":[{"Id":40,"TinyText":"FA 40 moved"}]}]}""";

        var (source, _) = FarsiContentMapper.GetEditSource(Master(legacy));

        Assert.Equal("EN meta", source.Metadata.Title);
        Assert.Equal("EN 40", Elements(source).Single(e => e.Id == 40).TinyText);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"Id":11,"Title":"other content"}""")]
    [InlineData("""{"Title":"no id"}""")]
    [InlineData("   ")]
    public void UnusableLegacy_FallsBackToEnglish(string legacy)
    {
        var (source, usedEnglishFallback) = FarsiContentMapper.GetEditSource(Master(legacy));

        Assert.True(usedEnglishFallback);
        Assert.Null(source.FarsiContent);
        Assert.Equal(("EN title", "EN meta", "EN 40"), (source.Title, source.Metadata.Title, Elements(source).Single(e => e.Id == 40).TinyText));
    }
}
