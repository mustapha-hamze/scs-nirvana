using System.Collections.Generic;
using System.Linq;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Web.Areas.BackOffice.Features.Content;
using Web.Areas.BackOffice.Features.Content.Contracts;
using Xunit;

namespace Web.Tests;

// The Farsi form keeps source and translation apart: the master is read-only display data, the
// editable/posted contract is translated text keyed by stable IDs only.
public sealed class FarsiContentMapperTests
{
    private static readonly Content Master = new()
    {
        Id = 10, TypeId = 1000, Title = "EN title",
        Metadata = new ContentMetadata { Id = 20, ContentId = 10, Title = "EN meta" },
        Sections = new List<ContentSection>
        {
            new() { Id = 30, ContentId = 10, Priority = 1, Elements = new List<SectionElement>
                {
                    new() { Id = 40, SectionId = 30, ElementType = 1000, TinyText = "EN 40", ElementTitle = "title-40", Size = 6 },
                    new() { Id = 41, SectionId = 30, ElementType = 1001, FileNameText = "img.jpg" }
                } }
        }
    };

    private static readonly LocalizedContentText Text = new("FA title", null, null, null, new LocalizedMetadataText(20, "FA meta", null, null, null),
        new List<LocalizedSectionText> { new(30, new List<LocalizedElementText> { new(40, "FA 40", null), new(41, null, null) }) });

    [Fact]
    public void PageModel_EditableFieldsHoldTranslation_SourceStaysMaster()
    {
        var model = FarsiContentMapper.ToPageModel(new ManualTranslationEditor(Master, "fp", TranslationStatus.Stale, ManualTranslationSeed.Legacy, Text), typeId: 1000);

        Assert.Equal((10, 1000, "fp", ManualTranslationSeed.Legacy, (TranslationStatus?)TranslationStatus.Stale),
            (model.Id, model.TypeId, model.SourceFingerprint, model.Seed, model.TranslationStatus));
        Assert.Equal(("FA title", 20, "FA meta"), (model.Title, model.Metadata.Id, model.Metadata.Title));
        Assert.Equal(new[] { (40, "FA 40"), (41, (string?)null) }, model.Sections.Single().SectionElements.Select(e => (e.Id, (string?)e.TinyText)));
        Assert.Same(Master, model.Source);
        Assert.Equal(("EN title", "EN 40", "title-40", "img.jpg"), (model.Source.Title, model.SourceElements[40].TinyText, model.SourceElements[40].ElementTitle, model.SourceElements[41].FileNameText));
    }

    [Fact]
    public void NoMasterMetadata_RendersNoMetadataFields()
    {
        var model = FarsiContentMapper.ToPageModel(new ManualTranslationEditor(new Content { Id = 10 }, "fp", null, ManualTranslationSeed.Source,
            new LocalizedContentText(null, null, null, null, null, new List<LocalizedSectionText>())), typeId: 1000);

        Assert.Null(model.Metadata);
        Assert.Empty(model.Sections);
    }

    [Fact]
    public void PostedForm_MapsToTextOnly()
    {
        var text = FarsiContentMapper.ToLocalizedText(new FarsiContentEditDto
        {
            Id = 10, Title = "FA", Metadata = new FarsiContentMetadataEditDto { Id = 20, Title = "FA meta" },
            Sections = new List<FarsiSectionEditDto> { new() { Id = 30, SectionElements = new List<FarsiSectionElementEditDto> { new() { Id = 40, TinyText = "FA 40" } } }, new() { Id = 31 } }
        });

        Assert.Equal(("FA", 20, "FA meta"), (text.Title, text.Metadata!.Id, text.Metadata.Title));
        Assert.Equal(new[] { 30, 31 }, text.Sections.Select(s => s.Id));
        Assert.Equal(new LocalizedElementText(40, "FA 40", null), text.Sections[0].Elements.Single());
        Assert.Empty(text.Sections[1].Elements);
        Assert.Empty(FarsiContentMapper.ToLocalizedText(new FarsiContentEditDto { Id = 10 }).Sections);
        Assert.Null(FarsiContentMapper.ToLocalizedText(new FarsiContentEditDto { Id = 10 }).Metadata);
    }

    // The request carries only IDs, the fingerprint and translatable text - never layout, media,
    // element titles/types or any other part of a Content graph.
    [Fact]
    public void RequestContract_IsTextOnly()
    {
        static IEnumerable<string> Names(System.Type type) => type.GetProperties().Select(p => p.Name).OrderBy(n => n);

        Assert.Equal(new[] { "Abstract", "Description", "HeadLine", "Id", "Metadata", "Sections", "SourceFingerprint", "Title" }, Names(typeof(FarsiContentEditDto)));
        Assert.Equal(new[] { "Author", "Description", "Id", "Keywords", "Title" }, Names(typeof(FarsiContentMetadataEditDto)));
        Assert.Equal(new[] { "Id", "SectionElements" }, Names(typeof(FarsiSectionEditDto)));
        Assert.Equal(new[] { "EditorText", "Id", "TinyText" }, Names(typeof(FarsiSectionElementEditDto)));
    }
}
