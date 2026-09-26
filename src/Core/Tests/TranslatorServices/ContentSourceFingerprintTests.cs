using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Xunit;

namespace Core.Tests.TranslatorServices;

public class ContentSourceFingerprintTests
{
    private static Content Source() => new()
    {
        Id = 7, TypeId = 1000, Title = "Title", HeadLine = "Head", Abstract = "Abstract", Description = "Description",
        FarsiContent = "{}", Categories = "1,2", Tags = "3", Cultures = "4", Status = 1,
        CreatedDT = new DateTime(2026, 1, 1), UpdatedDT = new DateTime(2026, 1, 2),
        Metadata = new ContentMetadata { Id = 3, ContentId = 7, Title = "Meta", Author = "Author", Keywords = "k1,k2", Description = "Meta description" },
        Images = new List<ContentImage> { new() { Id = 1, ImageFileName = "a.jpg" } },
        Sections = new List<ContentSection>
        {
            new()
            {
                Id = 20, Priority = 2, IsActive = true,
                Elements = new List<SectionElement>
                {
                    new() { Id = 202, ElementType = 1, Size = 12, IsActive = true, TinyText = "b", EditorText = "<p>b</p>", FileNameText = "b.png", GalleryImages = "x", ElementTitle = "B" },
                    new() { Id = 201, ElementType = 2, Size = 6, IsActive = true, TinyText = "a", EditorText = "<p>a</p>" },
                }
            },
            new() { Id = 10, Priority = 1, IsActive = true, Elements = new List<SectionElement> { new() { Id = 101, TinyText = "c" } } },
            new() { Id = 5, Priority = 2, IsActive = false, Elements = new List<SectionElement>() },
        }
    };

    private static void AssertChanges(Action<Content> change)
    {
        var content = Source();
        change(content);
        Assert.NotEqual(ContentSourceFingerprint.Compute(Source()), ContentSourceFingerprint.Compute(content));
    }

    private static void AssertUnchanged(Action<Content> change)
    {
        var content = Source();
        change(content);
        Assert.Equal(ContentSourceFingerprint.Compute(Source()), ContentSourceFingerprint.Compute(content));
    }

    [Fact]
    public void Compute_ReturnsLowercase64CharHex_AndIsStable()
    {
        var fingerprint = ContentSourceFingerprint.Compute(Source());

        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
        Assert.Equal(fingerprint, ContentSourceFingerprint.Compute(Source()));
    }

    [Fact]
    public void Compute_IgnoresLoadOrder_OfSectionsAndElements()
    {
        AssertUnchanged(c =>
        {
            c.Sections = c.Sections.Reverse().ToList();
            foreach (var section in c.Sections)
                section.Elements = section.Elements.Reverse().ToList();
        });
    }

    [Fact]
    public void Compute_ChangesWithSourceText()
    {
        AssertChanges(c => c.Title = "Other");
        AssertChanges(c => c.HeadLine = "Other");
        AssertChanges(c => c.Abstract = "Other");
        AssertChanges(c => c.Description = "Other");
        AssertChanges(c => c.TypeId = 1001);
        AssertChanges(c => c.Id = 8);
        // Exact values: whitespace and null-vs-empty are distinct sources.
        AssertChanges(c => c.Title = "Title ");
        AssertChanges(c => c.HeadLine = null);
        AssertChanges(c => c.HeadLine = "");
    }

    [Fact]
    public void Compute_ChangesWithMetadata()
    {
        AssertChanges(c => c.Metadata.Title = "Other");
        AssertChanges(c => c.Metadata.Author = "Other");
        AssertChanges(c => c.Metadata.Keywords = "Other");
        AssertChanges(c => c.Metadata.Description = "Other");
        AssertChanges(c => c.Metadata.Id = 4);
        AssertChanges(c => c.Metadata = null);
    }

    [Fact]
    public void Compute_ChangesWithStructureAndElementText()
    {
        AssertChanges(c => c.Sections.First(s => s.Id == 10).Priority = 3);
        AssertChanges(c => c.Sections.First(s => s.Id == 5).IsActive = true);
        AssertChanges(c => c.Sections.Remove(c.Sections.First(s => s.Id == 5)));
        AssertChanges(c => c.Sections.First(s => s.Id == 10).Elements.Add(new SectionElement { Id = 102 }));

        SectionElement Element(Content c) => c.Sections.First(s => s.Id == 20).Elements.First(e => e.Id == 202);
        AssertChanges(c => Element(c).TinyText = "Other");
        AssertChanges(c => Element(c).EditorText = "Other");
        AssertChanges(c => Element(c).ElementType = 9);
        AssertChanges(c => Element(c).Size = 9);
        AssertChanges(c => Element(c).IsActive = false);
    }

    [Fact]
    public void Compute_IgnoresExcludedFields()
    {
        AssertUnchanged(c =>
        {
            c.FarsiContent = "changed";
            c.Categories = c.Tags = c.Cultures = "changed";
            c.Status = 9;
            c.ApplicationId = 99;
            c.IsActive = !c.IsActive;
            c.CreatedDT = c.UpdatedDT = c.PublishDt = DateTime.UtcNow;
            c.Images = new List<ContentImage>();
            c.Metadata.UpdatedDT = DateTime.UtcNow;
            c.Metadata.IsActive = !c.Metadata.IsActive;
            foreach (var section in c.Sections)
            {
                section.UpdatedDT = DateTime.UtcNow;
                foreach (var element in section.Elements)
                {
                    element.FileNameText = "changed";
                    element.GalleryImages = "changed";
                    element.ElementTitle = "changed";
                    element.UpdatedDT = DateTime.UtcNow;
                    element.Status = 9;
                }
            }
        });
    }
}
