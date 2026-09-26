using System.Data.Common;
using System.Text.Json;
using Cms.ContentLocalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.ContentDelivery.PackageTests;

// A website's startup written against the packages only: bind ContentDelivery, register the SQL
// adapter and the opt-in health checks. The single test-host step swaps the storage engine: the
// adapter's DbContext options (found through EF's public DbContextOptions<> service type, never by
// the adapter's internal type name) become SQLite over a shared in-memory connection. Options
// validation, tenant binding and the metrics/cache decorator all run as packaged.
internal sealed class ConsumerHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly Type _contextType;

    public ConsumerHost(CmsDatabase database, IReadOnlyDictionary<string, string?> settings, CommandCounter? counter = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(settings.Append(new("ConnectionStrings:Cms", "Server=cms.invalid;Database=Cms")));

        builder.Services.AddSqlServerContentDelivery(builder.Configuration, builder.Configuration.GetConnectionString("Cms")!);
        builder.Services.AddHealthChecks().AddContentDeliveryHealthChecks();

        var options = builder.Services.Single(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>));
        _contextType = options.ServiceType.GenericTypeArguments[0];
        var sqlite = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(_contextType))!;
        sqlite.UseSqlite(database.Connection);
        if (counter != null)
            sqlite.AddInterceptors(counter);
        builder.Services.Remove(options);
        builder.Services.AddSingleton(options.ServiceType, sqlite.Options);

        _app = builder.Build();

        using var scope = _app.Services.CreateScope();
        ((DbContext)scope.ServiceProvider.GetRequiredService(_contextType)).Database.EnsureCreated();
    }

    public static ConsumerHost For(CmsDatabase database, int applicationId, bool cacheEnabled = false, string? legacyCulture = null, CommandCounter? counter = null) =>
        new(database, new Dictionary<string, string?>
        {
            ["ContentDelivery:ApplicationId"] = applicationId.ToString(),
            ["ContentDelivery:CacheEnabled"] = cacheEnabled.ToString(),
            ["ContentDelivery:LegacyFallbackEnabled"] = (legacyCulture != null).ToString(),
            ["ContentDelivery:LegacyFallbackCulture"] = legacyCulture
        }, counter);

    public IServiceProvider Services => _app.Services;

    public Type ContextType => _contextType;

    // One DI scope per read, as one web request would have.
    public async Task<T> Read<T>(Func<IContentDeliveryClient, Task<T>> read)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<IContentDeliveryClient>());
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}

// Counts database commands (round trips) the adapter issues.
internal sealed class CommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public override DbCommand CommandCreated(CommandEndEventData eventData, DbCommand result)
    {
        Interlocked.Increment(ref _count);
        return result;
    }
}

internal sealed record ContentSeed(int Id, int ApplicationId, int TypeId, string Title, DateTime UpdatedAt)
{
    public string? HeadLine { get; init; }
    public string? Abstract { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsDeleted { get; init; }
    public MetadataSeed? Metadata { get; init; }
    public IReadOnlyList<SectionSeed> Sections { get; init; } = [];
    public IReadOnlyList<ImageSeed> Images { get; init; } = [];
    public IReadOnlyList<int> CategoryIds { get; init; } = [];
    public IReadOnlyList<int> TagIds { get; init; } = [];
    public string? LegacySnapshot { get; init; }
}

internal sealed record MetadataSeed(int Id, string Title);

internal sealed record SectionSeed(int Id, int Priority, IReadOnlyList<ElementSeed> Elements, bool IsActive = true);

internal sealed record ElementSeed(int Id, int ElementType, string? TinyText, string? EditorText = null, bool IsActive = true, string? FileName = null);

internal sealed record ImageSeed(int Id, string FileName, int Size);

// The CMS tables, written the way BackOffice and the translation workflow write them. The adapter
// context refuses SaveChanges, so rows go in with SQL.
internal sealed class CmsDatabase : IDisposable
{
    public CmsDatabase()
    {
        Connection.Open();
    }

    public SqliteConnection Connection { get; } = new("DataSource=:memory:");

    public void Execute(string sql, params object?[] values)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++)
            command.Parameters.AddWithValue($"$p{i}", values[i] ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    // One transaction for a bulk seed (SQL, so Execute needs no transaction object).
    public void Transaction(Action seed)
    {
        Execute("BEGIN");
        seed();
        Execute("COMMIT");
    }

    public void Culture(int id, int applicationId, string key) =>
        Execute("INSERT INTO GNR_Cultures (Id, ApplicationId, Key, IsActive, IsDeleted) VALUES ($p0, $p1, $p2, 1, 0)", id, applicationId, key);

    public void Category(int id, int applicationId, int parentId, string title, bool isActive = true) =>
        Execute("INSERT INTO CMS_Categories (Id, ApplicationId, ParentId, Title, IsActive, IsDeleted) VALUES ($p0, $p1, $p2, $p3, $p4, 0)", id, applicationId, parentId, title, isActive);

    public void Tag(int id, int applicationId, string title) =>
        Execute("INSERT INTO GNR_Tags (Id, ApplicationId, Title, IsActive, IsDeleted) VALUES ($p0, $p1, $p2, 1, 0)", id, applicationId, title);

    public void Content(ContentSeed c)
    {
        Execute("""
            INSERT INTO CMS_Contents (Id, ApplicationId, TypeId, Title, HeadLine, Abstract, Description, PublishDt, FarsiContent, IsActive, IsDeleted, UpdatedDT)
            VALUES ($p0, $p1, $p2, $p3, $p4, $p5, $p6, $p7, $p8, $p9, $p10, $p11)
            """, c.Id, c.ApplicationId, c.TypeId, c.Title, c.HeadLine, c.Abstract, c.Description, c.UpdatedAt.Date, c.LegacySnapshot, c.IsActive, c.IsDeleted, c.UpdatedAt);
        if (c.Metadata is { } m)
            Execute("INSERT INTO CMS_ContentMetadata (Id, ContentId, Title, IsDeleted) VALUES ($p0, $p1, $p2, 0)", m.Id, c.Id, m.Title);
        foreach (var s in c.Sections)
        {
            Execute("INSERT INTO CMS_ContentSections (Id, ContentId, Priority, IsActive, IsDeleted) VALUES ($p0, $p1, $p2, $p3, 0)", s.Id, c.Id, s.Priority, s.IsActive);
            foreach (var e in s.Elements)
                Execute("""
                    INSERT INTO CMS_SectionElements (Id, SectionId, ElementType, ElementTitle, TinyText, EditorText, FileNameText, Size, IsActive, IsDeleted)
                    VALUES ($p0, $p1, $p2, $p3, $p4, $p5, $p6, 0, $p7, 0)
                    """, e.Id, s.Id, e.ElementType, $"Element {e.Id}", e.TinyText, e.EditorText, e.FileName, e.IsActive);
        }
        foreach (var i in c.Images)
            Execute("INSERT INTO CMS_ContentImages (Id, ContentId, ImageFileName, Size, IsActive, IsDeleted) VALUES ($p0, $p1, $p2, $p3, 1, 0)", i.Id, c.Id, i.FileName, i.Size);
        foreach (var categoryId in c.CategoryIds)
            Execute("INSERT INTO CMS_ContentInCategories (ContentId, CategoryId) VALUES ($p0, $p1)", c.Id, categoryId);
        foreach (var tagId in c.TagIds)
            Execute("INSERT INTO CMS_ContentInTags (ContentId, TagId) VALUES ($p0, $p1)", c.Id, tagId);
    }

    // A Ready translation of c's current source, each text passed through translate. A
    // fingerprint override makes it stale.
    public void Translation(int id, ContentSeed c, int cultureId, Func<string, string> translate, string? fingerprint = null)
    {
        string? T(string? text) => text == null ? null : translate(text);
        var text = new LocalizedText(T(c.Title), T(c.HeadLine), T(c.Abstract), T(c.Description),
            c.Metadata is { } m ? new LocalizedTextMetadata(m.Id, T(m.Title), null, null, null) : null,
            c.Sections.Select(s => new LocalizedTextSection(s.Id, s.Elements.Select(e => new LocalizedTextElement(e.Id, T(e.TinyText), T(e.EditorText))).ToList())).ToList());
        Execute("""
            INSERT INTO CMS_ContentTranslations (Id, ContentId, CultureId, TranslationStatus, SourceFingerprint, LocalizedTextJson, IsDeleted, UpdatedDT)
            VALUES ($p0, $p1, $p2, $p3, $p4, $p5, 0, $p6)
            """, id, c.Id, cultureId, SourceFingerprint.ReadyStatus, fingerprint ?? SourceFingerprint.Compute(Source(c)),
            JsonSerializer.Serialize(text, LocalizedTextParser.JsonOptions), c.UpdatedAt);
    }

    private static SourceContent Source(ContentSeed c) =>
        new(c.Id, c.TypeId, c.Title, c.HeadLine, c.Abstract, c.Description,
            c.Metadata is { } m ? new SourceMetadata(m.Id, m.Title, null, null, null) : null,
            c.Sections.Select(s => new SourceSection(s.Id, s.Priority, s.IsActive,
                s.Elements.Select(e => new SourceElement(e.Id, e.ElementType, 0, e.IsActive, e.TinyText, e.EditorText)).ToList())).ToList());

    public void Dispose() => Connection.Dispose();
}
