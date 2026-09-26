using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cms.ContentDelivery;
using Cms.ContentDelivery.SqlServer;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.Tests.ContentDelivery;

// Phase 4: the optional read cache, delivery metrics and health checks.
public sealed class ContentDeliveryResilienceTests : IDisposable
{
    private const int Tenant = 1;
    private static readonly DateTime Jan1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly SqliteContextFactory _factory = new();
    private readonly SqliteConnection _connection;
    private readonly List<IDisposable> _disposables = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Jan1));

    public ContentDeliveryResilienceTests()
    {
        using var core = _factory.CreateContext();
        _connection = (SqliteConnection)core.Database.GetDbConnection();
        core.AddRange(
            new Content { Id = 10, ApplicationId = Tenant, TypeId = 1, IsActive = true, Title = "Secret title", PublishDt = Jan1, UpdatedDT = Jan1, CreatedDT = Jan1 },
            new ContentSection { Id = 100, ContentId = 10, Priority = 1, IsActive = true, UpdatedDT = Jan1, CreatedDT = Jan1 },
            new SectionElement { Id = 1000, SectionId = 100, ElementType = 1, IsActive = true, TinyText = "tiny", UpdatedDT = Jan1, CreatedDT = Jan1 },
            new Culture { Id = 1, ApplicationId = 0, Key = "fa-IR", Title = "Farsi", IsActive = true, UpdatedDT = Jan1, CreatedDT = Jan1 },
            new ContentTranslation
            {
                Id = 400, ContentId = 10, CultureId = 1, TranslationStatus = TranslationStatus.Ready, SourceFingerprint = "stale-fingerprint",
                LocalizedTextJson = "{\"Title\":\"Translated\"}", Provider = "p", Model = "m", IsActive = true, UpdatedDT = Jan1, CreatedDT = Jan1
            });
        core.SaveChanges();
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
        _factory.Dispose();
    }

    private ContentDeliveryDbContext DeliveryContext()
    {
        var context = new ContentDeliveryDbContext(new DbContextOptionsBuilder<ContentDeliveryDbContext>().UseSqlite(_connection).Options);
        _disposables.Add(context);
        return context;
    }

    private MemoryContentDeliveryCache Cache() => new(_time, Ttl);

    private static IContentDeliveryClient Decorate(IContentDeliveryClient inner, IContentDeliveryCache cache, int applicationId = Tenant, ContentDeliveryMetrics metrics = null) =>
        new ContentDeliveryClientDecorator(inner, metrics ?? new ContentDeliveryMetrics(), cache, applicationId);

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string>("ContentDelivery:" + v.Key, v.Value)).Prepend(new("ContentDelivery:ApplicationId", "1")))
            .Build();

    private static ContentSummary Summary(int id, LocalizationSource source = LocalizationSource.Source) => new()
    {
        Id = id, Title = $"Title {id}",
        Localization = new LocalizationInfo { Source = source },
        Version = new DeliveryVersion { Tag = $"v{id}" }
    };

    private static ContentDocument Document(int id, LocalizationSource source = LocalizationSource.Source) => new()
    {
        Summary = Summary(id, source),
        Sections = new List<ContentDocumentSection> { new() { Id = 1, Elements = new List<ContentDocumentElement> { new() { Id = 2 } } } },
        Tags = new List<TaxonomyTerm> { new() { Id = 3 } }
    };

    // Counts inner reads; content ids above 100 are NotFound, culture "xx" is invalid, id 666 throws.
    private sealed class CountingClient : IContentDeliveryClient
    {
        public int Reads;

        public Task<ContentDeliveryResult<ContentDocument>> GetDocumentAsync(int contentId, string culture = null, CancellationToken cancellationToken = default)
        {
            Reads++;
            if (contentId == 666)
                throw new InvalidOperationException("boom");
            return Task.FromResult(culture == "xx" ? ContentDeliveryResult<ContentDocument>.InvalidCulture()
                : contentId > 100 ? ContentDeliveryResult<ContentDocument>.NotFound()
                : ContentDeliveryResult<ContentDocument>.Found(Document(contentId, culture == null ? LocalizationSource.Source : LocalizationSource.Translation)));
        }

        public Task<ContentDeliveryResult<IReadOnlyList<ContentDocument>>> GetDocumentSetAsync(IReadOnlyList<int> contentIds, string culture = null, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(ContentDeliveryResult<IReadOnlyList<ContentDocument>>.Found(contentIds.Select(id => Document(id)).ToList()));
        }

        public Task<ContentDeliveryResult<ContentPage<ContentSummary>>> GetListingAsync(ContentListingQuery query, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(ContentDeliveryResult<ContentPage<ContentSummary>>.Found(new ContentPage<ContentSummary>
            {
                Items = new List<ContentSummary> { Summary(1), Summary(2, LocalizationSource.LegacyFarsi) }, PageNumber = query.PageNumber, PageSize = query.PageSize, TotalCount = 2
            }));
        }

        public Task<ContentTaxonomy> GetTaxonomyAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(new ContentTaxonomy { Categories = new List<TaxonomyTerm> { new() { Id = 1 } } });
        }

        public Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<SitemapEntry>>(new List<SitemapEntry> { new() { ContentId = 1, Cultures = new List<string> { "fa-IR" } } });
        }
    }

    // ---- Configuration and registration ----

    [Fact]
    public void Cache_IsDisabledByDefault()
    {
        using var provider = new ServiceCollection().AddSqlServerContentDelivery(Configuration(), "Server=cms").BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Null(provider.GetRequiredService<ContentDeliveryTenant>().CacheTtl);
        Assert.Null(GetCache(Assert.IsType<ContentDeliveryClientDecorator>(scope.ServiceProvider.GetRequiredService<IContentDeliveryClient>())));
    }

    private static object GetCache(ContentDeliveryClientDecorator client) =>
        typeof(ContentDeliveryClientDecorator).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Single(f => f.FieldType == typeof(IContentDeliveryCache)).GetValue(client);

    [Fact]
    public void Cache_WhenEnabled_CapturesTheTtlAtStartup_AndSharesOneCacheAcrossScopes()
    {
        using var provider = new ServiceCollection()
            .AddSqlServerContentDelivery(Configuration(("CacheEnabled", "true"), ("CacheTtl", "00:00:45")), "Server=cms")
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.Equal(TimeSpan.FromSeconds(45), provider.GetRequiredService<ContentDeliveryTenant>().CacheTtl);
        var a = GetCache((ContentDeliveryClientDecorator)first.ServiceProvider.GetRequiredService<IContentDeliveryClient>());
        var b = GetCache((ContentDeliveryClientDecorator)second.ServiceProvider.GetRequiredService<IContentDeliveryClient>());
        Assert.NotNull(a);
        Assert.Same(a, b);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    [InlineData("00:05:01")]
    [InlineData("1.00:00:00")]
    public void Cache_TtlOutsideTheConservativeBound_FailsValidation(string ttl)
    {
        using var provider = new ServiceCollection()
            .AddContentDelivery(Configuration(("CacheEnabled", "true"), ("CacheTtl", ttl)))
            .BuildServiceProvider();

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ContentDeliveryOptions>>().Value);
        Assert.Contains("CacheTtl", error.Message);
    }

    // ---- Cache behaviour ----

    [Fact]
    public async Task Cache_HitsWithinTtl_AndReReadsAfterExpiry()
    {
        var inner = new CountingClient();
        var client = Decorate(inner, Cache());

        var first = (await client.GetDocumentAsync(1)).Value;
        var second = (await client.GetDocumentAsync(1)).Value;
        Assert.Equal(1, inner.Reads);
        Assert.Same(first, second);

        _time.SetUtcNow(new DateTimeOffset(Jan1) + Ttl - TimeSpan.FromTicks(1));
        await client.GetDocumentAsync(1);
        Assert.Equal(1, inner.Reads);

        _time.SetUtcNow(new DateTimeOffset(Jan1) + Ttl);
        await client.GetDocumentAsync(1);
        Assert.Equal(2, inner.Reads);
    }

    [Fact]
    public async Task Cache_CoversEveryReadShape()
    {
        var inner = new CountingClient();
        var client = Decorate(inner, Cache());

        for (var i = 0; i < 2; i++)
        {
            await client.GetDocumentAsync(1, "fa-IR");
            await client.GetDocumentSetAsync(new[] { 1, 2 }, "fa-IR");
            await client.GetListingAsync(new ContentListingQuery { TypeId = 1, Culture = "fa-IR" });
            await client.GetTaxonomyAsync();
            await client.GetSitemapEntriesAsync();
        }

        Assert.Equal(5, inner.Reads);
    }

    [Fact]
    public async Task Cache_IsolatesTenantsCulturesAndQueryShapes()
    {
        var inner = new CountingClient();
        var cache = Cache();
        var client = Decorate(inner, cache);
        var otherTenant = Decorate(inner, cache, applicationId: 2);

        var reads = new Func<Task>[]
        {
            () => client.GetDocumentAsync(1),
            () => otherTenant.GetDocumentAsync(1),
            () => client.GetDocumentAsync(1, "fa-IR"),
            () => client.GetDocumentAsync(1, "ar-SA"),
            () => client.GetDocumentAsync(2),
            () => client.GetDocumentSetAsync(new[] { 1 }),
            () => client.GetDocumentSetAsync(new[] { 1, 2 }),
            () => client.GetDocumentSetAsync(new[] { 2, 1 }),
            () => client.GetDocumentSetAsync(new[] { 1, 2 }, "fa-IR"),
            () => client.GetListingAsync(new ContentListingQuery()),
            () => client.GetListingAsync(new ContentListingQuery { TypeId = 1 }),
            () => client.GetListingAsync(new ContentListingQuery { CategoryId = 1 }),
            () => client.GetListingAsync(new ContentListingQuery { TagId = 1 }),
            () => client.GetListingAsync(new ContentListingQuery { PageNumber = 2 }),
            () => client.GetListingAsync(new ContentListingQuery { PageSize = 10 }),
            () => client.GetListingAsync(new ContentListingQuery { Culture = "fa-IR" }),
            () => client.GetTaxonomyAsync(),
            () => otherTenant.GetTaxonomyAsync(),
            () => client.GetSitemapEntriesAsync(),
            () => otherTenant.GetSitemapEntriesAsync()
        };

        foreach (var read in reads)
            await read();
        Assert.Equal(reads.Length, inner.Reads);

        foreach (var read in reads)
            await read();
        Assert.Equal(reads.Length, inner.Reads);
    }

    [Fact]
    public async Task Cache_NeverStoresNotFoundInvalidCultureOrExceptions()
    {
        var inner = new CountingClient();
        var client = Decorate(inner, Cache());

        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(ContentDeliveryStatus.NotFound, (await client.GetDocumentAsync(101)).Status);
            Assert.Equal(ContentDeliveryStatus.InvalidCulture, (await client.GetDocumentAsync(1, "xx")).Status);
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetDocumentAsync(666));
        }

        Assert.Equal(6, inner.Reads);
    }

    [Fact]
    public async Task CachedValues_CannotBeMutatedThroughTheirCollections()
    {
        var client = Decorate(new CountingClient(), Cache());

        var document = (await client.GetDocumentAsync(1)).Value;
        var page = (await client.GetListingAsync(new ContentListingQuery())).Value;
        var set = (await client.GetDocumentSetAsync(new[] { 1 })).Value;
        var taxonomy = await client.GetTaxonomyAsync();
        var sitemap = await client.GetSitemapEntriesAsync();

        Assert.Throws<NotSupportedException>(() => ((IList<ContentDocumentSection>)document.Sections).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ContentDocumentElement>)document.Sections[0].Elements).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TaxonomyTerm>)document.Tags).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ContentSummary>)page.Items).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ContentDocument>)set).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ContentDocumentSection>)set[0].Sections).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TaxonomyTerm>)taxonomy.Categories).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SitemapEntry>)sitemap).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)sitemap[0].Cultures).Clear());

        Assert.Single((await client.GetDocumentAsync(1)).Value.Sections);
        Assert.Equal(2, (await client.GetListingAsync(new ContentListingQuery())).Value.Items.Count);
    }

    [Fact]
    public void Cache_IsBounded()
    {
        var cache = Cache();
        for (var i = 0; i < MemoryContentDeliveryCache.MaxEntries + 10; i++)
            cache.Set($"k{i}", i);

        Assert.True(cache.TryGet("k0", out _));
        Assert.False(cache.TryGet($"k{MemoryContentDeliveryCache.MaxEntries}", out _));

        // Expired entries make room again.
        _time.SetUtcNow(new DateTimeOffset(Jan1) + Ttl);
        cache.Set("fresh", 1);
        Assert.True(cache.TryGet("fresh", out _));
    }

    [Fact]
    public async Task CachedAndUncachedReads_WriteNothing()
    {
        long Changes()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT total_changes();";
            return (long)command.ExecuteScalar();
        }

        var before = Changes();
        var context = DeliveryContext();
        var client = Decorate(new SqlContentDeliveryClient(context, new ContentDeliveryTenant(Tenant)), Cache());

        for (var i = 0; i < 2; i++)
        {
            Assert.True((await client.GetDocumentAsync(10, "fa-IR")).IsFound);
            await client.GetDocumentSetAsync(new[] { 10 }, "fa-IR");
            await client.GetListingAsync(new ContentListingQuery { Culture = "fa-IR" });
            await client.GetTaxonomyAsync();
            await client.GetSitemapEntriesAsync();
        }

        Assert.Equal(before, Changes());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    // ---- Delivery version ----

    [Fact]
    public async Task Version_ChangesWhenOnlyAChildRowChanges()
    {
        var before = (await new SqlContentDeliveryClient(DeliveryContext(), new ContentDeliveryTenant(Tenant)).GetDocumentAsync(10)).Value.Summary.Version;

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "UPDATE CMS_SectionElements SET TinyText = 'edited' WHERE Id = 1000;";
            command.ExecuteNonQuery();
        }

        var after = (await new SqlContentDeliveryClient(DeliveryContext(), new ContentDeliveryTenant(Tenant)).GetDocumentAsync(10)).Value.Summary.Version;
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.NotEqual(before.Tag, after.Tag);

        var again = (await new SqlContentDeliveryClient(DeliveryContext(), new ContentDeliveryTenant(Tenant)).GetDocumentAsync(10)).Value.Summary.Version;
        Assert.Equal(after.Tag, again.Tag);
    }

    // ---- Metrics ----

    private static readonly string[] AllowedTagKeys = { "operation", "outcome", "cache", "resolution" };

    private sealed record Measurement(string Instrument, double Value, Dictionary<string, object> Tags);

    // Listens only to meters created by this test's factory, so parallel tests do not interfere.
    private (ContentDeliveryMetrics Metrics, List<Measurement> Recorded) Metrics(Action onMeasurement = null)
    {
        var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _disposables.Add(services);
        var factory = services.GetRequiredService<IMeterFactory>();
        var recorded = new List<Measurement>();

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Scope == factory && instrument.Meter.Name == ContentDeliveryMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            lock (recorded)
                recorded.Add(new(instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
            onMeasurement?.Invoke();
        }
        listener.SetMeasurementEventCallback<double>((i, v, t, _) => Record(i, v, t));
        listener.SetMeasurementEventCallback<long>((i, v, t, _) => Record(i, v, t));
        listener.Start();
        _disposables.Add(listener);

        return (new ContentDeliveryMetrics(factory), recorded);
    }

    [Fact]
    public async Task Metrics_RecordDurationCacheResolutionAndOutcomes_WithApprovedTagsOnly()
    {
        var (metrics, recorded) = Metrics();
        var client = Decorate(new CountingClient(), Cache(), metrics: metrics);

        await client.GetDocumentAsync(1, "fa-IR");
        await client.GetDocumentAsync(1, "fa-IR");
        await client.GetDocumentAsync(101);
        await client.GetDocumentAsync(1, "xx");
        await client.GetListingAsync(new ContentListingQuery());
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetDocumentAsync(666));

        var durations = recorded.Where(m => m.Instrument == "cms.content_delivery.read.duration").ToList();
        Assert.Equal(6, durations.Count);
        Assert.All(durations, m => Assert.True(m.Value >= 0));
        Assert.Equal(
            new[] { ("document", "found", "miss"), ("document", "found", "hit"), ("document", "not_found", "miss"), ("document", "invalid_culture", "miss"), ("listing", "found", "miss"), ("document", "error", "miss") },
            durations.Select(m => ((string)m.Tags["operation"], (string)m.Tags["outcome"], (string)m.Tags["cache"])));

        var cache = recorded.Where(m => m.Instrument == "cms.content_delivery.cache.requests").Select(m => (string)m.Tags["cache"]).ToList();
        Assert.Equal(new[] { "miss", "hit", "miss", "miss", "miss", "miss" }, cache);

        var resolutions = recorded.Where(m => m.Instrument == "cms.content_delivery.localization.resolutions")
            .Select(m => ((string)m.Tags["operation"], (string)m.Tags["resolution"], m.Value)).ToList();
        Assert.Equal(new[] { ("document", "translation", 1d), ("document", "translation", 1d), ("listing", "source", 1d), ("listing", "legacy_farsi", 1d) }, resolutions);

        AssertOnlyApprovedTags(recorded, "fa-IR", "xx", "Title 1", "v1", "boom", "101", "666");
    }

    [Fact]
    public async Task Metrics_CacheDisabled_RecordsNoCacheLookups()
    {
        var (metrics, recorded) = Metrics();
        var client = new ContentDeliveryClientDecorator(new CountingClient(), metrics, null, Tenant);

        await client.GetTaxonomyAsync();

        Assert.Equal("disabled", recorded.Single(m => m.Instrument == "cms.content_delivery.read.duration").Tags["cache"]);
        Assert.DoesNotContain(recorded, m => m.Instrument == "cms.content_delivery.cache.requests");
    }

    [Fact]
    public async Task Metrics_CountRejectedTranslationsAndLegacySnapshots_WithoutContentOrIds()
    {
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "UPDATE CMS_Contents SET FarsiContent = '{\"Title\":\"no id\"}' WHERE Id = 10;";
            command.ExecuteNonQuery();
        }
        var (metrics, recorded) = Metrics();
        var client = new ContentDeliveryClientDecorator(
            new SqlContentDeliveryClient(DeliveryContext(), new ContentDeliveryTenant(Tenant, "fa-IR"), metrics), metrics, null, Tenant);

        var document = (await client.GetDocumentAsync(10, "fa-IR")).Value;

        Assert.Equal(LocalizationSource.Source, document.Summary.Localization.Source);
        Assert.Equal(new[] { "stale_translation", "invalid_legacy" },
            recorded.Where(m => m.Instrument == "cms.content_delivery.localization.fallbacks").Select(m => (string)m.Tags["outcome"]));
        AssertOnlyApprovedTags(recorded, "fa-IR", "Secret title", "stale-fingerprint", "Translated", "no id", "10", "400", Tenant.ToString());
    }

    [Fact]
    public async Task Metrics_AThrowingListener_DoesNotFailARead()
    {
        var (metrics, _) = Metrics(() => throw new InvalidOperationException("listener"));
        var client = Decorate(new CountingClient(), Cache(), metrics: metrics);

        Assert.True((await client.GetDocumentAsync(1)).IsFound);
        Assert.True((await client.GetDocumentAsync(1)).IsFound);
    }

    private static void AssertOnlyApprovedTags(List<Measurement> recorded, params string[] prohibited)
    {
        Assert.NotEmpty(recorded);
        foreach (var measurement in recorded)
        {
            Assert.All(measurement.Tags.Keys, key => Assert.Contains(key, AllowedTagKeys));
            foreach (var value in measurement.Tags.Values.Select(v => v?.ToString() ?? ""))
                Assert.All(prohibited, p => Assert.DoesNotContain(p, value));
        }
    }

    // ---- Health checks ----

    private static Task<HealthReport> CheckAsync(IConfiguration configuration, string name)
    {
        var services = new ServiceCollection().AddLogging().AddContentDelivery(configuration);
        services.AddHealthChecks().AddContentDeliveryHealthChecks();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(r => r.Name == name);
    }

    [Fact]
    public async Task ConfigurationHealth_IsHealthyForValidSettings()
    {
        var report = await CheckAsync(Configuration(), "content-delivery-configuration");

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Contains("content-delivery", report.Entries["content-delivery-configuration"].Tags);
    }

    [Fact]
    public async Task ConfigurationHealth_IsUnhealthy_NamingTheSettingButNotItsValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["ContentDelivery:ApplicationId"] = "-42", ["ContentDelivery:CacheTtl"] = "01:23:45" })
            .Build();

        var entry = (await CheckAsync(configuration, "content-delivery-configuration")).Entries["content-delivery-configuration"];

        Assert.Equal(HealthStatus.Unhealthy, entry.Status);
        Assert.Contains("configuration is invalid", entry.Description);
        Assert.Contains("ApplicationId", entry.Description);
        Assert.DoesNotContain("-42", entry.Description);
        Assert.Null(entry.Exception);
    }

    private static HealthCheckContext Context(IHealthCheck check) =>
        new() { Registration = new HealthCheckRegistration("content-delivery-database", check, HealthStatus.Unhealthy, null) };

    [Fact]
    public async Task DatabaseHealth_IsHealthyWhenTheDatabaseAcceptsAConnection_AndWritesNothing()
    {
        var context = DeliveryContext();
        var check = new ContentDeliveryDatabaseHealthCheck(context);

        var result = await check.CheckHealthAsync(Context(check));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DatabaseHealth_IsUnhealthyWhenUnavailable_WithoutConnectionDetails()
    {
        const string missing = "/nonexistent-content-delivery-dir/cms.db";
        using var context = new ContentDeliveryDbContext(new DbContextOptionsBuilder<ContentDeliveryDbContext>()
            .UseSqlite($"Data Source={missing};Mode=ReadOnly").Options);
        var check = new ContentDeliveryDatabaseHealthCheck(context);

        var result = await check.CheckHealthAsync(Context(check));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Content delivery database is unavailable.", result.Description);
        Assert.Null(result.Exception);
        Assert.DoesNotContain("nonexistent", result.Description);
    }

    [Fact]
    public async Task HealthChecks_DistinguishConfigurationFromDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["ContentDelivery:ApplicationId"] = "0" })
            .Build();
        var services = new ServiceCollection().AddLogging().AddContentDelivery(configuration);
        services.AddDbContext<ContentDeliveryDbContext>(o => o.UseSqlite(_connection));
        services.AddHealthChecks().AddContentDeliveryHealthChecks();
        using var provider = services.BuildServiceProvider();

        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Entries["content-delivery-configuration"].Status);
        Assert.Equal(HealthStatus.Healthy, report.Entries["content-delivery-database"].Status);
    }
}
