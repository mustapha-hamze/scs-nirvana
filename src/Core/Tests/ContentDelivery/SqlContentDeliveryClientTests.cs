using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cms.ContentDelivery;
using Cms.ContentDelivery.SqlServer;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.Tests.ContentDelivery;

// Exercises the real SQL adapter against the Core schema (created by ApplicationDbContext on
// SQLite and seeded through Core entities), so the adapter's table/column mapping is proven
// against Core's configuration rather than its own. SQLite stands in for SQL Server: predicates,
// ordering, paging and projections are provider-neutral LINQ; SQL Server-only behaviour (OPENJSON
// for id lists, datetime precision) is not exercised here.
public sealed class SqlContentDeliveryClientTests : IDisposable
{
    private const int Tenant = 1;
    private const int OtherTenant = 2;

    private static readonly DateTime Jan1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFactory _factory = new();
    private readonly SqliteConnection _connection;
    private readonly List<ContentDeliveryDbContext> _contexts = new();

    public SqlContentDeliveryClientTests()
    {
        using var core = _factory.CreateContext();
        _connection = (SqliteConnection)core.Database.GetDbConnection();
        Seed(core);
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
            context.Dispose();
        _factory.Dispose();
    }

    private ContentDeliveryDbContext CreateDeliveryContext()
    {
        var context = new ContentDeliveryDbContext(new DbContextOptionsBuilder<ContentDeliveryDbContext>().UseSqlite(_connection).Options);
        _contexts.Add(context);
        return context;
    }

    private SqlContentDeliveryClient Client(int applicationId = Tenant) =>
        new(CreateDeliveryContext(), new ContentDeliveryTenant(applicationId));

    // Tenant 1: contents 10, 11, 12, 15 visible; 13 inactive; 14 deleted.
    // Tenant 2: content 20, whose relation rows point at tenant 1's category and tag.
    private static void Seed(ApplicationDbContext db)
    {
        Content Content(int id, int app, int type, int days, bool active = true, bool deleted = false) => new()
        {
            Id = id, ApplicationId = app, TypeId = type, IsActive = active, IsDeleted = deleted,
            Title = $"Title {id}", HeadLine = $"HeadLine {id}", Abstract = $"Abstract {id}", Description = $"Description {id}",
            PublishDt = Jan1.AddDays(days), UpdatedDT = Jan1.AddDays(days), CreatedDT = Jan1
        };

        var c10 = Content(10, Tenant, 1, 4);
        c10.FarsiContent = "{\"Title\":\"Farsi title\"}";
        var c15 = Content(15, Tenant, 1, 0);
        c15.PublishDt = new DateTime(2099, 1, 1); // future PublishDt is not a visibility gate
        db.AddRange(c10, Content(11, Tenant, 1, 4), Content(12, Tenant, 2, 6), c15,
            Content(13, Tenant, 1, 9, active: false), Content(14, Tenant, 1, 9, deleted: true),
            Content(20, OtherTenant, 1, 9));

        ContentSection Section(int id, int content, int priority, bool active = true, bool deleted = false) =>
            new() { Id = id, ContentId = content, Priority = priority, IsActive = active, IsDeleted = deleted, UpdatedDT = Jan1, CreatedDT = Jan1 };
        db.AddRange(Section(100, 10, 2), Section(101, 10, 1), Section(102, 10, 1, active: false), Section(103, 10, 0, deleted: true),
            Section(110, 20, 1));

        SectionElement Element(int id, int section, bool active = true, bool deleted = false) => new()
        {
            Id = id, SectionId = section, ElementType = 1, IsActive = active, IsDeleted = deleted,
            ElementTitle = $"Element {id}", TinyText = "tiny", EditorText = "<p>x</p>", FileNameText = $"f{id}.pdf", GalleryImages = "g", Size = 3,
            UpdatedDT = Jan1, CreatedDT = Jan1
        };
        db.AddRange(Element(1001, 101), Element(1000, 101), Element(1002, 101, active: false), Element(1003, 101, deleted: true),
            Element(1004, 102), Element(1006, 103), Element(1005, 100), Element(1100, 110));

        ContentImage Image(int id, int content, int size, bool active = true, bool deleted = false) =>
            new() { Id = id, ContentId = content, ImageFileName = $"i{id}.jpg", Size = size, IsActive = active, IsDeleted = deleted, UpdatedDT = Jan1, CreatedDT = Jan1 };
        db.AddRange(Image(201, 10, 640), Image(200, 10, 430), Image(199, 10, 860, active: false), Image(198, 10, 860, deleted: true),
            Image(210, 20, 640));

        // Visible images without a usable file name: lower Ids than 10's valid ones (mixed), and
        // the only images of content 12 (invalid-only).
        ContentImage Unnamed(int id, int content, string fileName)
        {
            var image = Image(id, content, 640);
            image.ImageFileName = fileName;
            return image;
        }
        db.AddRange(Unnamed(190, 10, null), Unnamed(191, 10, ""), Unnamed(192, 10, "   "), Unnamed(193, 10, "\t\r\n"),
            Unnamed(220, 12, null), Unnamed(221, 12, ""), Unnamed(222, 12, " "), Unnamed(223, 12, "\t"));

        db.AddRange(
            new ContentMetadata { Id = 300, ContentId = 10, Title = "Meta", Author = "Author", Keywords = "k", Description = "d", IsActive = false, UpdatedDT = Jan1, CreatedDT = Jan1 },
            new ContentMetadata { Id = 301, ContentId = 11, Title = "Deleted meta", IsDeleted = true, UpdatedDT = Jan1, CreatedDT = Jan1 });

        Category Category(int id, int app, int parent, bool active = true, bool deleted = false) =>
            new() { Id = id, ApplicationId = app, ParentId = parent, Title = $"Category {id}", Description = $"About {id}", IsActive = active, IsDeleted = deleted, UpdatedDT = Jan1, CreatedDT = Jan1 };
        db.AddRange(Category(51, Tenant, 50), Category(50, Tenant, 0), Category(52, Tenant, 0, active: false), Category(53, Tenant, 0, deleted: true),
            Category(60, OtherTenant, 0));

        Tag Tag(int id, int app, bool active = true, bool deleted = false) =>
            new() { Id = id, ApplicationId = app, Title = $"Tag {id}", IsActive = active, IsDeleted = deleted, UpdatedDT = Jan1, CreatedDT = Jan1 };
        db.AddRange(Tag(70, Tenant), Tag(71, Tenant, active: false), Tag(72, Tenant, deleted: true), Tag(73, OtherTenant));

        var relation = 1;
        void InCategory(int content, params int[] categories) =>
            db.AddRange(categories.Select(c => new ContentInCategory { Id = relation++, ContentId = content, CategoryId = c, CreatedDt = Jan1 }));
        void InTag(int content, params int[] tags) =>
            db.AddRange(tags.Select(t => new ContentInTag { Id = relation++, ContentId = content, TagId = t }));

        InCategory(10, 51, 50, 52, 53, 60);
        InCategory(11, 50);
        InCategory(12, 51);
        InCategory(13, 50);
        InCategory(14, 50);
        InCategory(15, 50);
        InCategory(20, 60, 50);
        InTag(10, 70, 71, 72, 73);
        InTag(12, 70);
        InTag(20, 70, 73);

        // Content 10's fa-IR translation is stale ("fp") and its FarsiContent has no Id, so every
        // read below serves source text; LocalizedContentDeliveryTests covers resolution.
        db.Add(new Culture { Id = 1, ApplicationId = 0, Key = "fa-IR", Title = "Farsi", IsActive = true, UpdatedDT = Jan1, CreatedDT = Jan1 });
        db.Add(new ContentTranslation
        {
            Id = 400, ContentId = 10, CultureId = 1, TranslationStatus = TranslationStatus.Ready,
            SourceFingerprint = "fp", LocalizedTextJson = "{\"Title\":\"Translated\"}", Provider = "p", Model = "m",
            IsActive = true, UpdatedDT = Jan1, CreatedDT = Jan1
        });

        db.SaveChanges();
    }

    private static int[] Ids(IEnumerable<ContentSummary> items) => items.Select(i => i.Id).ToArray();

    [Fact]
    public async Task Document_ReturnsTheCompleteVisibleMasterGraph_InDeterministicOrder()
    {
        var result = await Client().GetDocumentAsync(10);

        Assert.True(result.TryGetValue(out var document));
        Assert.Equal(10, document.Summary.Id);
        Assert.Equal(1, document.Summary.TypeId);
        Assert.Equal("Title 10", document.Summary.Title);
        Assert.Equal("HeadLine 10", document.Summary.HeadLine);
        Assert.Equal("Abstract 10", document.Summary.Abstract);
        Assert.Equal("Description 10", document.Description);
        Assert.Equal(Jan1.AddDays(4), document.Summary.PublishedAt);
        Assert.Equal(Jan1.AddDays(4), document.Summary.Version.UpdatedAt);
        Assert.False(string.IsNullOrEmpty(document.Summary.Version.Tag));

        // Metadata IsActive is ignored; only deletion hides it.
        Assert.Equal("Meta", document.Metadata.Title);
        Assert.Equal("Author", document.Metadata.Author);

        // Sections Priority then Id; inactive (102) and deleted (103) sections and their elements
        // are gone; elements by Id without inactive (1002) or deleted (1003) ones.
        Assert.Equal(new[] { 101, 100 }, document.Sections.Select(s => s.Id));
        Assert.Equal(new[] { 1000, 1001 }, document.Sections[0].Elements.Select(e => e.Id));
        Assert.Equal(new[] { 1005 }, document.Sections[1].Elements.Select(e => e.Id));
        var element = document.Sections[0].Elements[0];
        Assert.Equal("Element 1000", element.Title);
        Assert.Equal("f1000.pdf", element.FileName);
        Assert.Equal("<p>x</p>", element.EditorText);

        // Images by Id, file name + size only; primary = lowest visible Id.
        Assert.Equal(new[] { 200, 201 }, document.Images.Select(i => i.Id));
        Assert.Equal("i200.jpg", document.Images[0].FileName);
        Assert.Equal(430, document.Images[0].Size);
        Assert.Equal(200, document.Summary.PrimaryImage.Id);

        // Only active, non-deleted, same-tenant terms; root ParentId 0 becomes null.
        Assert.Equal(new[] { (50, (int?)null), (51, 50) }, document.Categories.Select(c => (c.Id, c.ParentId)));
        Assert.Equal("About 50", document.Categories[0].Description);
        Assert.Equal(new[] { 70 }, document.Tags.Select(t => t.Id));
        Assert.Null(document.Tags[0].ParentId);
    }

    [Theory]
    [InlineData(13)]  // inactive
    [InlineData(14)]  // deleted
    [InlineData(20)]  // other tenant
    [InlineData(999)] // missing
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Document_IsNotFound_ForAnyContentOutsideTheVisibleTenantSet(int contentId)
    {
        var result = await Client().GetDocumentAsync(contentId);

        Assert.Equal(ContentDeliveryStatus.NotFound, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Document_OtherTenantSeesOnlyItsOwnGraph()
    {
        var document = (await Client(OtherTenant).GetDocumentAsync(20)).Value;

        Assert.Equal(new[] { 110 }, document.Sections.Select(s => s.Id));
        Assert.Equal(new[] { 210 }, document.Images.Select(i => i.Id));
        // Relation rows to tenant 1's category 50 and tag 70 are not honoured.
        Assert.Equal(new[] { 60 }, document.Categories.Select(c => c.Id));
        Assert.Equal(new[] { 73 }, document.Tags.Select(t => t.Id));
        Assert.Equal(ContentDeliveryStatus.NotFound, (await Client(OtherTenant).GetDocumentAsync(10)).Status);
    }

    [Fact]
    public async Task Document_WithDeletedMetadataAndNoChildren_HasEmptyGraph()
    {
        var document = (await Client().GetDocumentAsync(11)).Value;

        Assert.Null(document.Metadata);
        Assert.Null(document.Summary.PrimaryImage);
        Assert.Empty(document.Sections);
        Assert.Empty(document.Images);
        Assert.Empty(document.Tags);
        Assert.Equal(new[] { 50 }, document.Categories.Select(c => c.Id));
    }

    [Fact]
    public async Task Media_WithOnlyInvalidFileNames_YieldsNoImages()
    {
        var document = (await Client().GetDocumentAsync(12)).Value;
        var listed = (await Client().GetListingAsync(new ContentListingQuery { TypeId = 2 })).Value.Items.Single();

        Assert.Empty(document.Images);
        Assert.Null(document.Summary.PrimaryImage);
        Assert.Equal(12, listed.Id);
        Assert.Null(listed.PrimaryImage);
    }

    [Fact]
    public async Task Media_MixedValidAndInvalid_KeepsOnlyValid_LowestValidIdIsPrimary()
    {
        var document = (await Client().GetDocumentAsync(10)).Value;
        var listed = (await Client().GetListingAsync(new ContentListingQuery { TagId = 70 })).Value.Items.Single(i => i.Id == 10);
        var inSet = (await Client().GetDocumentSetAsync(new[] { 12, 10 })).Value;

        Assert.Equal(new[] { 200, 201 }, document.Images.Select(i => i.Id));
        Assert.All(document.Images, i => Assert.False(string.IsNullOrWhiteSpace(i.FileName)));
        Assert.Equal(200, document.Summary.PrimaryImage.Id);
        Assert.Equal("i200.jpg", listed.PrimaryImage.FileName);
        Assert.Empty(inSet[0].Images);
        Assert.Equal(new[] { 200, 201 }, inSet[1].Images.Select(i => i.Id));
    }

    [Fact]
    public async Task Document_IgnoresPublishDate()
    {
        Assert.True((await Client().GetDocumentAsync(15)).IsFound);
    }

    [Fact]
    public async Task DocumentSet_KeepsRequestedOrder_FirstDuplicate_AndSilentlyOmitsInvisibleIds()
    {
        var result = await Client().GetDocumentSetAsync(new[] { 12, 20, 10, 999, 12, 13, 14, -5, 10, 11 });

        Assert.True(result.TryGetValue(out var documents));
        Assert.Equal(new[] { 12, 10, 11 }, documents.Select(d => d.Summary.Id));
        // Children are attached to the right documents in a batch.
        Assert.Equal(new[] { 101, 100 }, documents[1].Sections.Select(s => s.Id));
        Assert.Empty(documents[0].Sections);
        Assert.Equal(new[] { 70 }, documents[0].Tags.Select(t => t.Id));
    }

    [Fact]
    public async Task DocumentSet_WithNoVisibleIds_IsFoundAndEmpty()
    {
        Assert.Empty((await Client().GetDocumentSetAsync(new[] { 20, 13 })).Value);
        Assert.Empty((await Client().GetDocumentSetAsync(Array.Empty<int>())).Value);
    }

    [Fact]
    public async Task DocumentSet_RejectsNullAndOversizeRequests()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Client().GetDocumentSetAsync(null));
        await Assert.ThrowsAsync<ArgumentException>(() => Client().GetDocumentSetAsync(Enumerable.Range(1, ContentDeliveryLimits.MaxDocumentSetSize + 1).ToArray()));
        Assert.True((await Client().GetDocumentSetAsync(Enumerable.Range(1, ContentDeliveryLimits.MaxDocumentSetSize).ToArray())).IsFound);
    }

    [Fact]
    public async Task Listing_OrdersByUpdatedThenIdDescending_WithDatabaseTotal()
    {
        var page = (await Client().GetListingAsync(new ContentListingQuery())).Value;

        // 12 (day 6), then 11 and 10 tie on day 4 -> Id desc, then 15 (day 0, future PublishDt).
        Assert.Equal(new[] { 12, 11, 10, 15 }, Ids(page.Items));
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(200, page.Items.Single(i => i.Id == 10).PrimaryImage.Id);
        Assert.Equal("i200.jpg", page.Items.Single(i => i.Id == 10).PrimaryImage.FileName);
        Assert.Null(page.Items.Single(i => i.Id == 11).PrimaryImage);
        Assert.All(page.Items, i => Assert.Equal(LocalizationSource.Source, i.Localization.Source));
    }

    [Theory]
    [InlineData(1, new[] { 12, 11 })]
    [InlineData(2, new[] { 10, 15 })]
    [InlineData(3, new int[0])]
    [InlineData(int.MaxValue, new int[0])]
    public async Task Listing_PagesAreOneBasedAndBounded(int pageNumber, int[] expected)
    {
        var page = (await Client().GetListingAsync(new ContentListingQuery { PageNumber = pageNumber, PageSize = 2 })).Value;

        Assert.Equal(expected, Ids(page.Items));
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(pageNumber, page.PageNumber);
        Assert.Equal(2, page.PageSize);
    }

    [Fact]
    public async Task Listing_FiltersCombineWithAnd_AndIgnoreOtherTenantsRelations()
    {
        var client = Client();

        // Content 20 (other tenant) links to category 50; 13/14 are inactive/deleted.
        Assert.Equal(new[] { 11, 10, 15 }, Ids((await client.GetListingAsync(new ContentListingQuery { CategoryId = 50 })).Value.Items));
        Assert.Equal(3, (await client.GetListingAsync(new ContentListingQuery { CategoryId = 50 })).Value.TotalCount);
        Assert.Equal(new[] { 12, 10 }, Ids((await client.GetListingAsync(new ContentListingQuery { TagId = 70 })).Value.Items));
        Assert.Equal(new[] { 12 }, Ids((await client.GetListingAsync(new ContentListingQuery { TypeId = 2 })).Value.Items));
        Assert.Equal(new[] { 10 }, Ids((await client.GetListingAsync(new ContentListingQuery { TypeId = 1, CategoryId = 51, TagId = 70 })).Value.Items));
        Assert.Empty((await client.GetListingAsync(new ContentListingQuery { TypeId = 2, CategoryId = 50 })).Value.Items);
    }

    [Theory]
    [InlineData(60, null)]   // other tenant's category (content 10 links to it)
    [InlineData(52, null)]   // inactive category
    [InlineData(53, null)]   // deleted category
    [InlineData(999, null)]  // missing category
    [InlineData(null, 73)]   // other tenant's tag
    [InlineData(null, 71)]   // inactive tag
    [InlineData(null, 72)]   // deleted tag
    public async Task Listing_ByInvisibleTerm_IsAnEmptyPage(int? categoryId, int? tagId)
    {
        var result = await Client().GetListingAsync(new ContentListingQuery { CategoryId = categoryId, TagId = tagId });

        Assert.True(result.IsFound);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    [Fact]
    public async Task Listing_OtherTenant_SeesOnlyItsOwnContent()
    {
        Assert.Equal(new[] { 20 }, Ids((await Client(OtherTenant).GetListingAsync(new ContentListingQuery())).Value.Items));
        Assert.Empty((await Client(OtherTenant).GetListingAsync(new ContentListingQuery { CategoryId = 50 })).Value.Items);
    }

    [Fact]
    public async Task Taxonomy_IsActiveTenantTermsById_WithNullRootParent()
    {
        var taxonomy = await Client().GetTaxonomyAsync();

        Assert.Equal(new[] { (50, (int?)null), (51, 50) }, taxonomy.Categories.Select(c => (c.Id, c.ParentId)));
        Assert.Equal("Category 50", taxonomy.Categories[0].Title);
        Assert.Equal("About 50", taxonomy.Categories[0].Description);
        Assert.Equal(new[] { 70 }, taxonomy.Tags.Select(t => t.Id));
        Assert.Equal("Tag 70", taxonomy.Tags[0].Title);
        Assert.Null(taxonomy.Tags[0].ParentId);

        var other = await Client(OtherTenant).GetTaxonomyAsync();
        Assert.Equal(new[] { 60 }, other.Categories.Select(c => c.Id));
        Assert.Equal(new[] { 73 }, other.Tags.Select(t => t.Id));

        Assert.Empty((await Client(3).GetTaxonomyAsync()).Categories);
    }

    [Fact]
    public async Task Sitemap_ListsVisibleTenantContentById()
    {
        var entries = await Client().GetSitemapEntriesAsync();

        Assert.Equal(new[] { 10, 11, 12, 15 }, entries.Select(e => e.ContentId));
        Assert.Equal(2, entries.Single(e => e.ContentId == 12).TypeId);
        Assert.Equal(Jan1.AddDays(6), entries.Single(e => e.ContentId == 12).LastModified);
        Assert.All(entries, e => Assert.Empty(e.Cultures));
        Assert.Equal(new[] { 20 }, (await Client(OtherTenant).GetSitemapEntriesAsync()).Select(e => e.ContentId));
    }

    [Theory]
    [InlineData("not a culture")]
    [InlineData("en_US")]
    [InlineData(" ")]
    [InlineData("en-US\n")]
    [InlineData("e")]
    [InlineData("fa-IR-x-too-many-parts")]
    public async Task MalformedCulture_IsRejectedBeforeAnyDataLookup(string culture)
    {
        // A disposed context throws if touched, so InvalidCulture proves no query ran.
        var context = CreateDeliveryContext();
        context.Dispose();
        var client = new SqlContentDeliveryClient(context, new ContentDeliveryTenant(Tenant));

        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await client.GetDocumentAsync(10, culture)).Status);
        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await client.GetDocumentSetAsync(new[] { 10 }, culture)).Status);
        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await client.GetListingAsync(new ContentListingQuery { Culture = culture })).Status);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("fa-IR", "fa-IR")]
    [InlineData("FA-ir", "fa-IR")]
    public async Task WithoutAUsableTranslation_EveryShapeServesMasterSourceText(string culture, string reportedCulture)
    {
        // Content 10 has a stale Ready translation and an invalid legacy snapshot: neither is served.
        var document = (await Client().GetDocumentAsync(10, culture)).Value;
        var listed = (await Client().GetListingAsync(new ContentListingQuery { Culture = culture, TypeId = 1 })).Value.Items.Single(i => i.Id == 10);
        var inSet = (await Client().GetDocumentSetAsync(new[] { 10 }, culture)).Value.Single();

        foreach (var summary in new[] { document.Summary, listed, inSet.Summary })
        {
            Assert.Equal("Title 10", summary.Title);
            Assert.Equal(LocalizationSource.Source, summary.Localization.Source);
            Assert.Equal(reportedCulture, summary.Localization.Culture);
        }
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("zh-Hant-TW")]
    public async Task WellFormedButUnknownCulture_IsInvalid(string culture)
    {
        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await Client().GetDocumentAsync(10, culture)).Status);
        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await Client().GetDocumentSetAsync(new[] { 10 }, culture)).Status);
        Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await Client().GetListingAsync(new ContentListingQuery { Culture = culture })).Status);
    }

    [Fact]
    public void AdapterModel_MapsNoTranslationInternals()
    {
        var model = CreateDeliveryContext().Model;

        Assert.DoesNotContain(model.GetEntityTypes(), e => e.GetTableName() is "CMS_ContentTranslationJobs" or "CMS_ContentTranslationBackfillCheckpoints" or "CMS_ContentInCultures");
        Assert.DoesNotContain(model.GetEntityTypes().SelectMany(e => e.GetProperties()),
            p => p.Name is "Provider" or "Model" or "Error" or "TranslatedAt" or "Prompt");
    }

    // The adapter maps the Core tables independently; this fails if Core renames a table or
    // column the adapter reads.
    [Fact]
    public void AdapterModel_MatchesCoreTableAndColumnNames()
    {
        using var core = _factory.CreateContext();
        var coreColumns = core.Model.GetEntityTypes()
            .Where(e => e.GetTableName() != null)
            .GroupBy(e => e.GetTableName())
            .ToDictionary(g => g.Key, g => g.SelectMany(e => e.GetProperties()).Select(p => p.GetColumnName()).ToHashSet());

        var adapterTables = CreateDeliveryContext().Model.GetEntityTypes().ToList();
        Assert.Equal(11, adapterTables.Count);
        foreach (var entity in adapterTables)
        {
            var table = entity.GetTableName();
            Assert.True(coreColumns.ContainsKey(table), $"Core has no table {table}.");
            foreach (var property in entity.GetProperties())
                Assert.True(coreColumns[table].Contains(property.GetColumnName()), $"Core table {table} has no column {property.GetColumnName()}.");
        }
    }

    [Fact]
    public async Task EveryOperation_IsReadOnly()
    {
        long Changes()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT total_changes();";
            return (long)command.ExecuteScalar();
        }

        var before = Changes();
        var context = CreateDeliveryContext();
        var client = new SqlContentDeliveryClient(context, new ContentDeliveryTenant(Tenant));

        await client.GetDocumentAsync(10, "fa-IR");
        await client.GetDocumentSetAsync(new[] { 10, 11, 12 }, "fa-IR");
        await client.GetListingAsync(new ContentListingQuery { CategoryId = 50, Culture = "fa-IR" });
        await client.GetTaxonomyAsync();
        await client.GetSitemapEntriesAsync();

        Assert.Equal(before, Changes());
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }
}

public class SqlServerContentDeliveryRegistrationTests
{
    private static IConfiguration Configuration(string applicationId) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["ContentDelivery:ApplicationId"] = applicationId })
            .Build();

    [Fact]
    public void Registration_BindsTenantAndSqlServerConnection_ToAScopedClient()
    {
        using var provider = new ServiceCollection()
            .AddSqlServerContentDelivery(Configuration("7"), "Server=cms;Database=Cms;Integrated Security=true")
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IContentDeliveryClient>();
        var context = scope.ServiceProvider.GetRequiredService<ContentDeliveryDbContext>();
        var tenant = provider.GetRequiredService<ContentDeliveryTenant>();

        Assert.IsType<SqlContentDeliveryClient>(client);
        Assert.Equal(7, tenant.ApplicationId);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.Contains("Initial Catalog=Cms", context.Database.GetConnectionString());
        Assert.Equal(QueryTrackingBehavior.NoTracking, context.ChangeTracker.QueryTrackingBehavior);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Registration_RequiresAConnectionString(string connectionString)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new ServiceCollection().AddSqlServerContentDelivery(Configuration("7"), connectionString));
    }

    [Fact]
    public void Registration_RejectsASecondDeliveryRegistration()
    {
        var services = new ServiceCollection().AddContentDelivery(Configuration("7"));

        Assert.Throws<InvalidOperationException>(() => services.AddSqlServerContentDelivery(Configuration("8"), "Server=cms"));
    }
}
