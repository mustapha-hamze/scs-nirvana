using Application.CMSRepository;
using Application.Contracts.CMS;
using Application.GeneralRepository;
using Application.Mapper;
using Application.UseCases.CMSServices;
using Application.UseCases.TranslatorServices;
using AutoMapper;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Infrastructure.Data;
using Infrastructure.Mapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Core.Tests.CMSServices;

// End-to-end over SQLite with the real repositories and UnitOfWork: source mutations mark
// mismatching ContentTranslation rows Stale in the same transaction, and nothing else.
public class ContentTranslationStalenessTests : IDisposable
{
    private const int ApplicationId = 1;
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private int _contentId, _sectionId, _elementId, _metadataId;

    public void Dispose() => _factory.Dispose();

    // Content with metadata and one section/element, plus a Ready translation whose fingerprint
    // matches the seeded source.
    private async Task Seed(bool withMetadata = true)
    {
        await using var context = _factory.CreateContext();
        var content = new Content
        {
            ApplicationId = ApplicationId, TypeId = 1000, Title = "Title", HeadLine = "Head", IsActive = true,
            Metadata = withMetadata ? new ContentMetadata { Title = "Meta", Author = "Author" } : null,
            Sections = new List<ContentSection>
            {
                new() { Priority = 1, IsActive = true, Elements = new List<SectionElement> { new() { TinyText = "tiny", EditorText = "<p>x</p>", IsActive = true } } }
            }
        };
        context.Contents.Add(content);
        await context.SaveChangesAsync();
        _contentId = content.Id;
        _sectionId = content.Sections.Single().Id;
        _elementId = content.Sections.Single().Elements.Single().Id;
        _metadataId = content.Metadata?.Id ?? 0;

        var fingerprint = ContentSourceFingerprint.Compute(await new ContentTranslationRepository(context).GetSourceGraph(_contentId));
        context.ContentTranslations.Add(new ContentTranslation
        {
            ContentId = _contentId, CultureId = 1, TranslationStatus = TranslationStatus.Ready, SourceFingerprint = fingerprint,
            LocalizedTextJson = "{\"title\":\"ترجمه\"}", Provider = "openai", Model = "gpt", TranslatedAt = SeededAt, Error = "none",
            IsActive = true, CreatedDT = SeededAt, UpdatedDT = SeededAt
        });
        await context.SaveChangesAsync();
    }

    private ContentServices CreateSut(ApplicationDbContext context, IContentTranslationRepository translationRepository = null)
    {
        var mapper = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); }, NullLoggerFactory.Instance).CreateMapper();
        return new ContentServices(
            new ContentQueryRepository(context), new ContentCommandRepository(context),
            new Mock<IContentRelationRepository>().Object, new Mock<IContentsInCategoryQueryAdapter>().Object,
            new Mock<ICategoryRepository>().Object, new Mock<ITagRepository>().Object, new Mock<ICultureRepository>().Object,
            mapper, new Infrastructure.UnitOfWork.UnitOfWork(context, NullLogger<Infrastructure.UnitOfWork.UnitOfWork>.Instance),
            translationRepository ?? new ContentTranslationRepository(context));
    }

    private async Task<ContentTranslation> Translation()
    {
        await using var context = _factory.CreateContext();
        return await context.ContentTranslations.IgnoreQueryFilters().SingleAsync(t => t.ContentId == _contentId);
    }

    private async Task Run(Func<ContentServices, Task> action)
    {
        await using var context = _factory.CreateContext();
        await action(CreateSut(context));
    }

    public static TheoryData<string> SourceMutations => new()
    {
        "Update", "CreateMetadata", "UpdateMetadata", "CreateSection", "DeleteSection", "UpdateSectionPriority",
        "CreateSectionElement", "UpdateSectionElement"
    };

    [Theory]
    [MemberData(nameof(SourceMutations))]
    public async Task SourceMutation_MarksMismatchingTranslationStale_AndPreservesPayload(string mutation)
    {
        await Seed(withMetadata: mutation != "CreateMetadata");
        var before = await Translation();

        await Run(sut => mutation switch
        {
            "Update" => sut.Update(new ContentDto { Id = _contentId, TypeId = 1000, Title = "New title", HeadLine = "Head", IsActive = true }, ApplicationId),
            "CreateMetadata" => sut.CreateContentMetadata(new ContentMetadataDto { ContentId = _contentId, Title = "Meta" }, ApplicationId),
            "UpdateMetadata" => sut.UpdateContentMetadata(new ContentMetadataDto { Id = _metadataId, Title = "New meta", Author = "Author" }, ApplicationId),
            "CreateSection" => sut.CreateSection(new SectionDto { ContentId = _contentId, Priority = 2 }, ApplicationId),
            "DeleteSection" => sut.DeleteSection(_sectionId, ApplicationId),
            "UpdateSectionPriority" => sut.UpdateSectionPriority(_sectionId, 5, ApplicationId),
            "CreateSectionElement" => sut.CreateSectionElement(new SectionElementDto { SectionId = _sectionId, TinyText = "more" }, ApplicationId),
            "UpdateSectionElement" => sut.UpdateSectionElement(new SectionElementDto { Id = _elementId, TinyText = "changed", EditorText = "<p>x</p>" }, ApplicationId),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        });

        var after = await Translation();
        Assert.Equal(TranslationStatus.Stale, after.TranslationStatus);
        Assert.Equal(before.SourceFingerprint, after.SourceFingerprint);
        Assert.Equal(before.LocalizedTextJson, after.LocalizedTextJson);
        Assert.Equal(before.Provider, after.Provider);
        Assert.Equal(before.Model, after.Model);
        Assert.Equal(before.Error, after.Error);
        Assert.Equal(before.TranslatedAt, after.TranslatedAt);
        Assert.Equal(before.CreatedDT, after.CreatedDT);
        Assert.Equal(before.IsActive, after.IsActive);
        Assert.False(after.IsDeleted);
    }

    [Fact]
    public async Task FileOnlyElementUpdate_LeavesMatchingTranslationUntouched()
    {
        await Seed();

        await Run(sut => sut.UpdateSectionElement(new SectionElementDto { Id = _elementId, TinyText = "tiny", EditorText = "<p>x</p>", FileNameText = "new.png", GalleryImages = "g" }, ApplicationId));

        var after = await Translation();
        Assert.Equal(TranslationStatus.Ready, after.TranslationStatus);
        Assert.Equal(SeededAt, after.UpdatedDT);
    }

    [Fact]
    public async Task NonSourceOperations_DoNotEvaluateStaleness()
    {
        await Seed();
        await using (var context = _factory.CreateContext())
        {
            // A mismatching fingerprint: only a staleness evaluation could flip this row.
            var translation = await context.ContentTranslations.SingleAsync();
            translation.SourceFingerprint = new string('0', 64);
            await context.SaveChangesAsync();
        }

        await Run(sut => sut.ChangeContentActiveMode(_contentId, false, ApplicationId));
        await Run(sut => sut.UpdateTranslate(_contentId, "{}", ApplicationId));
        await Run(sut => sut.ActivateTranslatedContent(_contentId, "{}", ApplicationId));
        await Run(sut => sut.ActivateExistingContent(_contentId, ApplicationId));
        await Run(sut => sut.CreateContentImage(new ContentImageDto { ContentId = _contentId, ImageFileName = "a.jpg" }, ApplicationId));
        await Run(sut => sut.DeleteAllContentImages(_contentId, ApplicationId));
        await Run(sut => sut.CreateContentCategories(new List<int>(), _contentId, ApplicationId));

        Assert.Equal(TranslationStatus.Ready, (await Translation()).TranslationStatus);
    }

    [Fact]
    public async Task SoftDeletedTranslation_IsNotTouched()
    {
        await Seed();
        await using (var context = _factory.CreateContext())
        {
            context.ContentTranslations.Remove(await context.ContentTranslations.SingleAsync());
            await context.SaveChangesAsync();
        }

        await Run(sut => sut.UpdateSectionPriority(_sectionId, 5, ApplicationId));

        var after = await Translation();
        Assert.True(after.IsDeleted);
        Assert.Equal(TranslationStatus.Ready, after.TranslationStatus);
    }

    [Fact]
    public async Task StalenessFailure_RollsBackSourceChange()
    {
        await Seed();
        await using var context = _factory.CreateContext();
        var realRepository = new ContentTranslationRepository(context);
        var failing = new Mock<IContentTranslationRepository>();
        failing.Setup(r => r.GetTranslations(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken ct) => realRepository.GetTranslations(id, ct));
        failing.Setup(r => r.GetSourceGraph(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated"));

        // CreateSection saves inside the transaction (for its generated id) before the failure.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut(context, failing.Object).CreateSection(new SectionDto { ContentId = _contentId, Priority = 2 }, ApplicationId));

        await using var verifyContext = _factory.CreateContext();
        Assert.Equal(1, await verifyContext.ContentSections.CountAsync(s => s.ContentId == _contentId));
        Assert.Equal(TranslationStatus.Ready, (await Translation()).TranslationStatus);
    }
}
