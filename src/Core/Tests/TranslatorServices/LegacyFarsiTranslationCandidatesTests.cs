using Application.CMSRepository;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities;
using Domains.Entities.General;
using Infrastructure.CMSRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Extensions;
using Xunit;

namespace Core.Tests.TranslatorServices;

// Over SQLite with the real candidate repository. No translation port is involved at all.
public class LegacyFarsiTranslationCandidatesTests : IDisposable
{
    private const int ApplicationId = 1;
    private const string LegacyFarsi = "{\"Title\":\"legacy\"}";
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private int _cultureId;

    public void Dispose() => _factory.Dispose();

    private async Task<int> Add(object entity)
    {
        await using var context = _factory.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync();
        return ((BaseEntity)entity).Id;
    }

    private async Task SeedCulture(bool isActive = true, bool isDeleted = false) =>
        _cultureId = await Add(new Culture { ApplicationId = ApplicationId, Title = "Farsi", Key = "fa-IR", IsActive = isActive, IsDeleted = isDeleted });

    private Task<int> AddContent(string title = "English", int typeId = 1001, string farsi = LegacyFarsi, int applicationId = ApplicationId,
        bool isActive = true, bool isDeleted = false, DateTime? updated = null) =>
        Add(new Content
        {
            ApplicationId = applicationId, TypeId = typeId, Title = title, FarsiContent = farsi, IsActive = isActive, IsDeleted = isDeleted,
            UpdatedDT = updated ?? Now
        });

    private async Task<string> Fingerprint(int contentId)
    {
        await using var context = _factory.CreateContext();
        return ContentSourceFingerprint.Compute(await new ContentTranslationJobRepository(context).FindSourceGraph(contentId));
    }

    private Task AddJob(int contentId, string fingerprint, ContentTranslationJobState state, int? cultureId = null) =>
        Add(new ContentTranslationJob
        {
            ContentId = contentId, CultureId = cultureId ?? _cultureId, SourceFingerprint = fingerprint, State = state, NextAttemptAt = Now, IsActive = true
        });

    private ContentTranslationOptions Options(int? cultureId = null) => new()
    {
        ActivationCultureId = cultureId ?? _cultureId,
        LegacyBulkCandidateTypeIds = [1001, 1002]
    };

    private async Task<LegacyFarsiTranslationCandidatePage> Find(LegacyFarsiTranslationCandidateQuery query = null, ContentTranslationOptions options = null,
        int applicationId = ApplicationId)
    {
        await using var context = _factory.CreateContext();
        var sut = new LegacyFarsiTranslationCandidates(new LegacyFarsiTranslationCandidateRepository(context), options ?? Options());
        var page = await sut.Find(query ?? new LegacyFarsiTranslationCandidateQuery(), applicationId);
        Assert.Empty(context.ChangeTracker.Entries());
        return page;
    }

    private async Task<int[]> Ids(LegacyFarsiTranslationCandidateQuery query = null) =>
        (await Find(query)).Items.Select(i => i.ContentId).ToArray();

    [Fact]
    public async Task ListsOnlyTheApplicationsConfiguredTypeContentWithNonBlankLegacyFarsi()
    {
        await SeedCulture();
        var active = await AddContent(typeId: 1001);
        var inactive = await AddContent(typeId: 1002, isActive: false);
        var foreign = await AddContent(applicationId: 2);
        await AddContent(isDeleted: true);
        await AddContent(typeId: 2000);
        await AddContent(farsi: null);
        await AddContent(farsi: "");
        await AddContent(farsi: "  \t\r\n ");

        var page = await Find();

        Assert.True(page.CultureAvailable);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(
            [new LegacyFarsiTranslationCandidate(active, "English", 1001, true, Now), new LegacyFarsiTranslationCandidate(inactive, "English", 1002, false, Now)],
            page.Items);
        Assert.Equal([foreign], (await Find(applicationId: 2)).Items.Select(i => i.ContentId));
        Assert.Empty((await Find(new LegacyFarsiTranslationCandidateQuery(ContentId: foreign))).Items);
    }

    [Fact]
    public async Task ExcludesContentWithATranslationRowInAnyStateIncludingDeleted()
    {
        await SeedCulture();
        var otherCulture = await Add(new Culture { ApplicationId = ApplicationId, Title = "German", Key = "de-DE", IsActive = true });
        foreach (var status in Enum.GetValues<TranslationStatus>())
            await Add(new ContentTranslation { ContentId = await AddContent(), CultureId = _cultureId, TranslationStatus = status, SourceFingerprint = "fp", LocalizedTextJson = "{}", IsActive = true });
        await Add(new ContentTranslation
        {
            ContentId = await AddContent(), CultureId = _cultureId, TranslationStatus = TranslationStatus.Ready, SourceFingerprint = "fp", LocalizedTextJson = "{}", IsDeleted = true
        });
        var otherCultureOnly = await AddContent();
        await Add(new ContentTranslation { ContentId = otherCultureOnly, CultureId = otherCulture, TranslationStatus = TranslationStatus.Ready, SourceFingerprint = "fp", LocalizedTextJson = "{}" });

        Assert.Equal([otherCultureOnly], await Ids());
    }

    [Fact]
    public async Task ExcludesOnlyQueuedOrProcessingJobsForTheCurrentFingerprintAndCulture()
    {
        await SeedCulture();
        var otherCulture = await Add(new Culture { ApplicationId = ApplicationId, Title = "German", Key = "de-DE", IsActive = true });

        // A full source graph proves the projected fingerprint matches ContentSourceFingerprint's.
        var queued = await AddContent();
        await Add(new ContentMetadata { ContentId = queued, Title = "Meta", Author = "A", Keywords = "k", Description = "d" });
        var section = await Add(new ContentSection { ContentId = queued, Priority = 2, IsActive = true });
        await Add(new SectionElement { SectionId = section, ElementType = 1, Size = 12, IsActive = true, TinyText = "t", EditorText = "<p>e</p>" });
        await Add(new ContentSection { ContentId = queued, Priority = 1, IsDeleted = true });
        await AddJob(queued, await Fingerprint(queued), ContentTranslationJobState.Queued);

        var processing = await AddContent();
        await AddJob(processing, await Fingerprint(processing), ContentTranslationJobState.Processing);

        var oldFingerprint = await AddContent();
        await AddJob(oldFingerprint, "old-fingerprint", ContentTranslationJobState.Queued);

        List<int> included = [oldFingerprint];
        foreach (var terminal in new[] { ContentTranslationJobState.Succeeded, ContentTranslationJobState.Failed, ContentTranslationJobState.Superseded })
        {
            var id = await AddContent();
            await AddJob(id, await Fingerprint(id), terminal);
            included.Add(id);
        }

        var deletedJob = await AddContent();
        await Add(new ContentTranslationJob
        {
            ContentId = deletedJob, CultureId = _cultureId, SourceFingerprint = await Fingerprint(deletedJob), State = ContentTranslationJobState.Queued,
            NextAttemptAt = Now, IsDeleted = true
        });
        included.Add(deletedJob);

        var otherCultureJob = await AddContent();
        await AddJob(otherCultureJob, await Fingerprint(otherCultureJob), ContentTranslationJobState.Queued, otherCulture);
        included.Add(otherCultureJob);

        var page = await Find(new LegacyFarsiTranslationCandidateQuery(PageSize: 2));

        Assert.Equal(included.Count, page.TotalCount);
        Assert.Equal(included.Take(2), page.Items.Select(i => i.ContentId));
        Assert.Equal(included, await Ids(new LegacyFarsiTranslationCandidateQuery(PageSize: 100)));
    }

    [Fact]
    public async Task FiltersByAllowedTypeTitleAndContentId()
    {
        await SeedCulture();
        var news = await AddContent("Weekly news", typeId: 1001);
        var report = await AddContent("Annual report", typeId: 1002);
        var otherNews = await AddContent("News flash", typeId: 1002);

        Assert.Equal([news], await Ids(new LegacyFarsiTranslationCandidateQuery(TypeId: 1001)));
        Assert.Empty(await Ids(new LegacyFarsiTranslationCandidateQuery(TypeId: 2000)));
        Assert.Equal([report], await Ids(new LegacyFarsiTranslationCandidateQuery(Title: "  report ")));
        Assert.Equal([otherNews], await Ids(new LegacyFarsiTranslationCandidateQuery(TypeId: 1002, Title: "News")));
        Assert.Equal([report], await Ids(new LegacyFarsiTranslationCandidateQuery(ContentId: report)));
        Assert.Empty(await Ids(new LegacyFarsiTranslationCandidateQuery(TypeId: 1001, ContentId: report)));
        Assert.Equal([news, report, otherNews], await Ids(new LegacyFarsiTranslationCandidateQuery(Title: "   ")));
    }

    [Fact]
    public async Task SortsDeterministicallyWithIdTieBreakerAndPagesServerSide()
    {
        await SeedCulture();
        var b1 = await AddContent("B", typeId: 1002, updated: Now.AddDays(-1));
        var a = await AddContent("A", typeId: 1002, updated: Now.AddDays(-3));
        var b2 = await AddContent("B", typeId: 1001, updated: Now.AddDays(-1));
        var c = await AddContent("C", typeId: 1001, updated: Now.AddDays(-2));

        Assert.Equal([a, b1, b2, c], await Ids(new LegacyFarsiTranslationCandidateQuery(Sort: LegacyFarsiCandidateSort.Title)));
        Assert.Equal([c, b1, b2, a], await Ids(new LegacyFarsiTranslationCandidateQuery(Sort: LegacyFarsiCandidateSort.Title, Descending: true)));
        Assert.Equal([b2, c, b1, a], await Ids(new LegacyFarsiTranslationCandidateQuery(Sort: LegacyFarsiCandidateSort.TypeId)));
        Assert.Equal([b1, b2, c, a], await Ids(new LegacyFarsiTranslationCandidateQuery(Sort: LegacyFarsiCandidateSort.UpdatedAt, Descending: true)));
        Assert.Equal([c, b2, a, b1], await Ids(new LegacyFarsiTranslationCandidateQuery(Descending: true)));

        var second = await Find(new LegacyFarsiTranslationCandidateQuery(Sort: LegacyFarsiCandidateSort.Title, Page: 2, PageSize: 2));
        Assert.Equal((4, 2, 2), (second.TotalCount, second.Page, second.PageSize));
        Assert.Equal([b2, c], second.Items.Select(i => i.ContentId));

        var beyond = await Find(new LegacyFarsiTranslationCandidateQuery(Page: 3, PageSize: 2));
        Assert.Equal(4, beyond.TotalCount);
        Assert.Empty(beyond.Items);

        var clamped = await Find(new LegacyFarsiTranslationCandidateQuery(Page: 0, PageSize: 1000));
        Assert.Equal((1, LegacyFarsiTranslationCandidates.MaxPageSize, 4), (clamped.Page, clamped.PageSize, clamped.Items.Count));
        Assert.Equal(1, (await Find(new LegacyFarsiTranslationCandidateQuery(PageSize: 0))).PageSize);
    }

    [Theory]
    [InlineData("unset")]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("inactive")]
    public async Task UnavailableCulture_ReturnsNoCandidates(string culture)
    {
        await SeedCulture(isActive: culture != "inactive", isDeleted: culture == "deleted");
        await AddContent();
        var cultureId = culture switch { "unset" => 0, "missing" => _cultureId + 100, _ => _cultureId };

        var page = await Find(options: Options(cultureId));

        Assert.False(page.CultureAvailable);
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Reads_WriteNothing()
    {
        await SeedCulture();
        var content = await AddContent();
        await AddJob(content, await Fingerprint(content), ContentTranslationJobState.Queued);
        var other = await AddContent();
        await AddJob(other, "old-fingerprint", ContentTranslationJobState.Failed);

        async Task<object> Snapshot()
        {
            await using var context = _factory.CreateContext();
            return string.Join("|",
                string.Join(",", await context.Contents.IgnoreQueryFilters().OrderBy(c => c.Id).Select(c => $"{c.Id}:{c.FarsiContent}:{c.UpdatedDT:O}").ToListAsync()),
                string.Join(",", await context.ContentTranslationJobs.OrderBy(j => j.Id).Select(j => $"{j.Id}:{j.State}:{j.Version}:{j.AttemptCount}").ToListAsync()),
                await context.ContentTranslations.IgnoreQueryFilters().CountAsync());
        }

        var before = await Snapshot();
        Assert.Equal([other], await Ids());
        Assert.Equal(before, await Snapshot());
    }

    [Fact]
    public void HasNoTranslationPortDependency()
    {
        var parameters = typeof(LegacyFarsiTranslationCandidates).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);

        Assert.Equal(new[] { typeof(ILegacyFarsiTranslationCandidateRepository), typeof(ContentTranslationOptions) }, parameters);
    }

    [Theory]
    [InlineData(new[] { 1112, 1001 }, 25, true)]
    [InlineData(new int[0], 1, true)]
    [InlineData(new[] { 1001, 1001 }, 25, false)]
    [InlineData(new[] { 0 }, 25, false)]
    [InlineData(new[] { -5 }, 25, false)]
    [InlineData(new[] { 1001 }, 0, false)]
    [InlineData(new[] { 1001 }, 101, false)]
    public void LegacyBulkSettings_AreBoundAndValidated(int[] typeIds, int maxItems, bool valid)
    {
        var settings = new Dictionary<string, string> { ["ContentTranslation:BulkRequestMaxItems"] = maxItems.ToString() };
        for (var i = 0; i < typeIds.Length; i++)
            settings[$"ContentTranslation:LegacyBulkCandidateTypeIds:{i}"] = typeIds[i].ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddCmsServices();
        using var provider = services.BuildServiceProvider();

        var options = () => provider.GetRequiredService<IOptions<ContentTranslationOptions>>().Value;

        if (!valid)
        {
            Assert.Throws<OptionsValidationException>(options);
            return;
        }
        Assert.Equal(typeIds, options().LegacyBulkCandidateTypeIds);
        Assert.Equal(maxItems, options().BulkRequestMaxItems);
    }
}
