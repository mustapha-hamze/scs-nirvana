using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cms.ContentDelivery.SqlServer.Tests;

// Smoke-level compatibility check: the adapter's queries translate and materialize on .NET 9 +
// EF Core 9. Delivery rules themselves are covered in depth by Core.Tests
// (SqlContentDeliveryClientTests); this only proves the net9 runtime path works.
public sealed class Net9RuntimeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ContentDeliveryDbContext _db;
    private readonly SqlContentDeliveryClient _client;

    public Net9RuntimeTests()
    {
        _connection.Open();
        _db = new ContentDeliveryDbContext(new DbContextOptionsBuilder<ContentDeliveryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        // The adapter context refuses SaveChanges, so seed with SQL.
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CMS_Contents (Id, ApplicationId, TypeId, Title, PublishDt, IsActive, IsDeleted, UpdatedDT) VALUES
              (1, 1, 1, 'One',   '2026-01-01', 1, 0, '2026-01-02'),
              (2, 1, 1, 'Two',   '2026-01-01', 1, 0, '2026-01-03'),
              (3, 2, 1, 'Other', '2026-01-01', 1, 0, '2026-01-04');
            INSERT INTO CMS_ContentSections (Id, ContentId, Priority, IsActive, IsDeleted) VALUES (10, 1, 1, 1, 0);
            INSERT INTO CMS_SectionElements (Id, SectionId, ElementType, ElementTitle, Size, IsActive, IsDeleted) VALUES (100, 10, 1, 'E', 0, 1, 0);
            INSERT INTO CMS_ContentImages (Id, ContentId, ImageFileName, Size, IsActive, IsDeleted) VALUES
              (50, 1, ' ', 640, 1, 0), (51, 1, 'a.jpg', 640, 1, 0);
            INSERT INTO CMS_ContentMetadata (Id, ContentId, Title, IsDeleted) VALUES (60, 1, 'Meta', 0);
            INSERT INTO CMS_Categories (Id, ApplicationId, ParentId, Title, IsActive, IsDeleted) VALUES (70, 1, 0, 'Cat', 1, 0);
            INSERT INTO GNR_Tags (Id, ApplicationId, Title, IsActive, IsDeleted) VALUES (80, 1, 'Tag', 1, 0);
            INSERT INTO CMS_ContentInCategories (Id, ContentId, CategoryId) VALUES (1, 1, 70), (2, 3, 70);
            INSERT INTO CMS_ContentInTags (Id, ContentId, TagId) VALUES (1, 1, 80);
            """;
        command.ExecuteNonQuery();

        _client = new SqlContentDeliveryClient(_db, new ContentDeliveryTenant(1));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void RunsOnNet9WithEfCore9()
    {
        Assert.Equal(9, Environment.Version.Major);
        Assert.Equal(9, typeof(DbContext).Assembly.GetName().Version!.Major);
        Assert.Equal(9, typeof(RelationalDatabaseFacadeExtensions).Assembly.GetName().Version!.Major);
    }

    [Fact]
    public async Task Document()
    {
        Assert.True((await _client.GetDocumentAsync(1, "fa-IR")).TryGetValue(out var document));

        Assert.Equal("One", document.Summary.Title);
        Assert.Equal(100, document.Sections.Single().Elements.Single().Id);
        Assert.Equal(51, document.Summary.PrimaryImage!.Id);
        Assert.Equal(new[] { 51 }, document.Images.Select(i => i.Id));
        Assert.Equal("Meta", document.Metadata!.Title);
        Assert.Null(document.Categories.Single().ParentId);
        Assert.Equal(80, document.Tags.Single().Id);
        Assert.Equal(ContentDeliveryStatus.NotFound, (await _client.GetDocumentAsync(3)).Status);
    }

    [Fact]
    public async Task DocumentSet()
    {
        var documents = (await _client.GetDocumentSetAsync(new[] { 2, 3, 1, 2 })).Value!;

        Assert.Equal(new[] { 2, 1 }, documents.Select(d => d.Summary.Id));
    }

    [Fact]
    public async Task Listing()
    {
        var page = (await _client.GetListingAsync(new ContentListingQuery { CategoryId = 70, TagId = 80, PageSize = 1 })).Value!;
        var all = (await _client.GetListingAsync(new ContentListingQuery())).Value!;

        Assert.Equal(1, page.Items.Single().Id);
        Assert.Equal(51, page.Items.Single().PrimaryImage!.Id);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(new[] { 2, 1 }, all.Items.Select(i => i.Id));
        Assert.Equal(2, all.TotalCount);
    }
}
