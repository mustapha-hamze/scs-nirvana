using Application.Contracts.CMSApi;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.TranslatorServices;

// Over SQLite with the real LocalizedContentReadRepository.
public class LocalizedContentReaderTests : IDisposable
{
    private const int ApplicationId = 1;
    private const int OtherApplicationId = 2;

    private readonly SqliteContextFactory _factory = new();
    private int _contentId, _sectionId, _textElementId, _fileElementId, _metadataId;
    private int _farsiCultureId, _germanCultureId, _otherAppCultureId;

    public void Dispose() => _factory.Dispose();

    private ContentTranslationOptions Options(bool enabled = true, int[] applicationIds = null, bool legacy = true) => new()
    {
        LocalizedReadEnabled = enabled,
        LocalizedReadApplicationIds = applicationIds ?? [ApplicationId],
        LegacyFarsiCultureId = legacy ? _farsiCultureId : 0
    };

    private async Task Seed(bool legacyFarsi = false)
    {
        await using var context = _factory.CreateContext();
        var farsi = new Culture { ApplicationId = ApplicationId, Title = "Farsi", Key = "fa-IR", IsActive = true };
        var german = new Culture { ApplicationId = ApplicationId, Title = "German", Key = "de-DE", IsActive = true };
        var otherApp = new Culture { ApplicationId = OtherApplicationId, Title = "Farsi", Key = "fa-IR", IsActive = true };
        var content = new Content
        {
            ApplicationId = ApplicationId, TypeId = 1000, Title = "English", HeadLine = "Head", Description = "<p>desc</p>",
            Categories = "1", Tags = "2", Cultures = "3",
            Metadata = new ContentMetadata { Title = "Meta", Author = "Author" },
            Images = new List<ContentImage> { new() { ImageFileName = "img.jpg", Size = 1 } },
            Sections = new List<ContentSection>
            {
                new()
                {
                    Priority = 1, Elements = new List<SectionElement>
                    {
                        new() { ElementType = 1000, TinyText = "tiny", ElementTitle = "title" },
                        new() { ElementType = 1003, FileNameText = "file.pdf", GalleryImages = "a.jpg" }
                    }
                }
            }
        };
        context.AddRange(farsi, german, otherApp, content);
        await context.SaveChangesAsync();
        _contentId = content.Id;
        _metadataId = content.Metadata.Id;
        _sectionId = content.Sections.Single().Id;
        _textElementId = content.Sections.Single().Elements.Single(e => e.ElementType == 1000).Id;
        _fileElementId = content.Sections.Single().Elements.Single(e => e.ElementType == 1003).Id;
        (_farsiCultureId, _germanCultureId, _otherAppCultureId) = (farsi.Id, german.Id, otherApp.Id);

        if (legacyFarsi)
        {
            content.FarsiContent = LegacyJson();
            await context.SaveChangesAsync();
        }
    }

    private string Payload(string title = "عنوان", bool includeFileElement = true) => LegacyFarsiContentParser.Serialize(new LocalizedContentText(
        title, "سر", null, "<p>شرح</p>", new LocalizedMetadataText(_metadataId, "متا", "نویسنده", null, null),
        [new LocalizedSectionText(_sectionId, includeFileElement
            ? [new LocalizedElementText(_textElementId, "کوچک", null), new LocalizedElementText(_fileElementId, null, null)]
            : [new LocalizedElementText(_textElementId, "کوچک", null)])]));

    private string LegacyJson() =>
        $"{{\"Id\":{_contentId},\"Title\":\"قدیمی\",\"Sections\":[{{\"Id\":{_sectionId},\"Elements\":[{{\"Id\":{_textElementId},\"TinyText\":\"قدیم\"}}]}}]}}";

    private async Task<string> Fingerprint()
    {
        await using var context = _factory.CreateContext();
        return ContentSourceFingerprint.Compute(await new ContentTranslationJobRepository(context).FindSourceGraph(_contentId));
    }

    private async Task AddTranslation(int cultureId, TranslationStatus status = TranslationStatus.Ready, string fingerprint = null,
        string payload = null, bool deleted = false)
    {
        fingerprint ??= await Fingerprint();
        await using var context = _factory.CreateContext();
        context.ContentTranslations.Add(new ContentTranslation
        {
            ContentId = _contentId, CultureId = cultureId, TranslationStatus = status, SourceFingerprint = fingerprint,
            LocalizedTextJson = payload ?? Payload(), Provider = "manual", Error = "secret-error", IsActive = true, IsDeleted = deleted
        });
        await context.SaveChangesAsync();
    }

    private async Task<LocalizedContentReadResult> Read(string culture = "fa-IR", ContentTranslationOptions options = null,
        int applicationId = ApplicationId, int? contentId = null)
    {
        await using var context = _factory.CreateContext();
        var result = await new LocalizedContentReader(new LocalizedContentReadRepository(context), options ?? Options())
            .Read(contentId ?? _contentId, applicationId, culture);
        Assert.Empty(context.ChangeTracker.Entries()); // read-only: nothing tracked, nothing staged
        return result;
    }

    private static void AssertEnglish(LocalizedContentApiDto content)
    {
        Assert.Equal(LocalizedContentResolution.Source, content.Resolution);
        Assert.Equal("English", content.Title);
        Assert.Equal("Meta", content.Metadata.Title);
        Assert.Equal("tiny", content.Sections.Single().Elements.Single(e => e.ElementType == 1000).TinyText);
    }

    [Fact]
    public async Task MatchingReadyTranslation_IsProjectedOverMasterLayout()
    {
        await Seed(legacyFarsi: true);
        await AddTranslation(_farsiCultureId);

        var result = await Read("FA-ir");

        Assert.Equal(LocalizedContentReadStatus.Found, result.Status);
        var content = result.Content;
        Assert.Equal(LocalizedContentResolution.Translation, content.Resolution);
        Assert.Equal("fa-IR", content.Culture);
        Assert.Equal("عنوان", content.Title);
        Assert.Null(content.Abstract);
        Assert.Equal("نویسنده", content.Metadata.Author);
        var elements = content.Sections.Single().Elements;
        Assert.Equal("کوچک", elements.Single(e => e.Id == _textElementId).TinyText);
        Assert.Equal("title", elements.Single(e => e.Id == _textElementId).ElementTitle);
        var file = elements.Single(e => e.Id == _fileElementId);
        Assert.Equal(("file.pdf", "a.jpg"), (file.FileNameText, file.GalleryImages));
        Assert.Equal("img.jpg", content.Images.Single().ImageFileName);
        Assert.Equal(("1", "2", "3"), (content.Categories, content.Tags, content.Cultures));
    }

    public static TheoryData<string> RejectedTranslations => new() { "stale-fingerprint", "stale-status", "deleted", "invalid-json", "missing-node", "wrong-culture" };

    [Theory]
    [MemberData(nameof(RejectedTranslations))]
    public async Task UnusableTranslation_IsNeverServed(string kind)
    {
        await Seed();
        switch (kind)
        {
            case "stale-fingerprint": await AddTranslation(_germanCultureId, fingerprint: "old"); break;
            case "stale-status": await AddTranslation(_germanCultureId, TranslationStatus.Stale); break;
            case "deleted": await AddTranslation(_germanCultureId, deleted: true); break;
            case "invalid-json": await AddTranslation(_germanCultureId, payload: "{\"title\":"); break;
            case "missing-node": await AddTranslation(_germanCultureId, payload: Payload(includeFileElement: false)); break;
            case "wrong-culture": await AddTranslation(_farsiCultureId); break;
        }

        var result = await Read("de-DE");

        Assert.Equal("de-DE", result.Content.Culture);
        AssertEnglish(result.Content);
    }

    [Fact]
    public async Task LegacyFarsi_IsRebasedOntoCurrentMaster_WhenNoUsableTranslation()
    {
        await Seed(legacyFarsi: true);
        await AddTranslation(_farsiCultureId, fingerprint: "old");

        var content = (await Read()).Content;

        Assert.Equal(LocalizedContentResolution.LegacyFarsi, content.Resolution);
        Assert.Equal("قدیمی", content.Title);
        Assert.Equal("قدیم", content.Sections.Single().Elements.Single(e => e.Id == _textElementId).TinyText);
        // Metadata absent from the snapshot keeps master text.
        Assert.Equal("Meta", content.Metadata.Title);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"Id\":999999,\"Title\":\"x\"}")]
    [InlineData("{\"Id\":ID,\"Sections\":[{\"Id\":999999}]}")]
    public async Task InvalidLegacyFarsi_FallsBackToEnglish(string legacy)
    {
        await Seed();
        await using (var context = _factory.CreateContext())
        {
            var content = await context.Contents.SingleAsync();
            content.FarsiContent = legacy.Replace("ID", content.Id.ToString());
            await context.SaveChangesAsync();
        }

        AssertEnglish((await Read()).Content);
    }

    [Fact]
    public async Task LegacyFarsi_IsNeverServedForAnotherCulture()
    {
        await Seed(legacyFarsi: true);

        AssertEnglish((await Read("de-DE")).Content);
        // Not configured as the legacy culture: no fallback even for fa-IR.
        AssertEnglish((await Read(options: Options(legacy: false))).Content);
    }

    [Theory]
    [InlineData("xx-XX")]
    [InlineData("fa")]
    public async Task UnknownCulture_FallsBackToEnglish(string culture)
    {
        await Seed(legacyFarsi: true);

        var content = (await Read(culture)).Content;

        Assert.Null(content.Culture);
        AssertEnglish(content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fa_IR")]
    [InlineData("<script>")]
    [InlineData("f")]
    public async Task InvalidCulture_IsRejected(string culture)
    {
        await Seed();
        Assert.Equal(LocalizedContentReadStatus.InvalidCulture, (await Read(culture)).Status);
    }

    [Fact]
    public async Task RolloutGates_DefaultOff_AndPerApplication()
    {
        await Seed();
        await AddTranslation(_farsiCultureId);

        Assert.False(new ContentTranslationOptions().LocalizedReadEnabled);
        Assert.Empty(new ContentTranslationOptions().LocalizedReadApplicationIds);
        Assert.Equal(LocalizedContentReadStatus.Disabled, (await Read(options: Options(enabled: false))).Status);
        Assert.Equal(LocalizedContentReadStatus.Disabled, (await Read(options: Options(applicationIds: [OtherApplicationId]))).Status);
        // Gate is checked before culture validation, so a disabled app reveals nothing else.
        Assert.Equal(LocalizedContentReadStatus.Disabled, (await Read("bad culture", Options(enabled: false))).Status);
    }

    [Fact]
    public async Task OtherApplication_CannotReadContentOrUseItsCultures()
    {
        await Seed();
        await AddTranslation(_otherAppCultureId);

        Assert.Equal(LocalizedContentReadStatus.NotFound,
            (await Read(options: Options(applicationIds: [ApplicationId, OtherApplicationId]), applicationId: OtherApplicationId)).Status);
        Assert.Equal(LocalizedContentReadStatus.NotFound, (await Read(contentId: 999999)).Status);
        // fa-IR resolves to this application's culture, so the other app's translation is ignored.
        AssertEnglish((await Read(options: Options(legacy: false))).Content);
    }
}
