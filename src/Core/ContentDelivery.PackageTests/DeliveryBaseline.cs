using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Cms.ContentDelivery.PackageTests;

// Non-gating performance baseline for the two heaviest reads - a maximum-size document set and a
// maximum-page-size filtered listing - through the packaged adapter over a seeded local SQLite
// database. It asserts no timings. `./run.sh baseline` rewrites BASELINE.md with the results;
// otherwise they only go to the test output.
[Trait("Category", "Baseline")]
public sealed class DeliveryBaseline(ITestOutputHelper output)
{
    private const int Tenant = 1, FaIR = 1, ArticleType = 2, Category = 11;
    private const int Contents = 2_000, OtherTenantContents = 500, SectionsPerContent = 4, ElementsPerSection = 5, ImagesPerContent = 3;
    private const int Warmup = 5, Iterations = 30;

    [Fact]
    public async Task Measure()
    {
        using var db = new CmsDatabase();
        await using (ConsumerHost.For(db, Tenant))
        {
            // The first host only creates the schema.
        }
        Seed(db);

        var set = Enumerable.Range(1, ContentDeliveryLimits.MaxDocumentSetSize).Select(i => i * 37 % Contents + 1).Distinct().ToList();
        Assert.Equal(ContentDeliveryLimits.MaxDocumentSetSize, set.Count);
        var listing = new ContentListingQuery { TypeId = ArticleType, CategoryId = Category, PageNumber = 3, PageSize = ContentDeliveryLimits.MaxPageSize };

        var rows = new List<string>();
        foreach (var cacheEnabled in new[] { false, true })
        {
            var counter = new CommandCounter();
            await using var host = ConsumerHost.For(db, Tenant, cacheEnabled, counter: counter);
            var cache = cacheEnabled ? "enabled (steady-state hits)" : "disabled";
            foreach (var culture in new string?[] { null, "fa-IR" })
            {
                rows.Add(await Run($"Document set, {set.Count} ids", culture ?? "source", cache, counter, host,
                    async c => Assert.Equal(set.Count, (await c.GetDocumentSetAsync(set, culture)).Value!.Count)));
                var query = listing with { Culture = culture };
                rows.Add(await Run($"Listing, page {query.PageNumber} x {query.PageSize}", culture ?? "source", cache, counter, host,
                    async c => Assert.Equal(query.PageSize, (await c.GetListingAsync(query)).Value!.Items.Count)));
            }
        }

        var report = Report(rows);
        output.WriteLine(report);
        if (Environment.GetEnvironmentVariable("CONTENT_DELIVERY_BASELINE_OUT") is { Length: > 0 } path)
            await File.WriteAllTextAsync(path, report);
    }

    private static async Task<string> Run(string read, string culture, string cache, CommandCounter counter, ConsumerHost host, Func<IContentDeliveryClient, Task> operation)
    {
        for (var i = 0; i < Warmup; i++)
            await host.Read(async c => { await operation(c); return 0; });

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var times = new double[Iterations];
        var commands = counter.Count;
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        for (var i = 0; i < Iterations; i++)
        {
            var started = Stopwatch.GetTimestamp();
            await host.Read(async c => { await operation(c); return 0; });
            times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
        var bytes = (GC.GetTotalAllocatedBytes(precise: true) - allocated) / Iterations;
        var queries = (counter.Count - commands) / (double)Iterations;

        Array.Sort(times);
        return $"| {read} | {culture} | {cache} | {times[Iterations / 2]:0.00} | {times[(int)Math.Ceiling(Iterations * 0.95) - 1]:0.00} | {bytes / 1024.0:0} | {queries:0.#} |";
    }

    // Tenant: Contents articles, all in one category, a third tagged; each with metadata,
    // SectionsPerContent sections x ElementsPerSection elements (one inactive per section) and
    // ImagesPerContent images. Half carry a Ready fa-IR translation, a tenth a stale one. A second
    // tenant adds OtherTenantContents rows the tenant filters must skip.
    private static void Seed(CmsDatabase db) => db.Transaction(() =>
    {
        db.Culture(FaIR, 0, "fa-IR");
        db.Culture(2, 0, "en-US");
        db.Category(Category, Tenant, 0, "Topic");
        db.Category(12, 2, 0, "Other");
        db.Tag(30, Tenant, "Tag");

        var start = new DateTime(2026, 1, 1);
        for (var id = 1; id <= Contents + OtherTenantContents; id++)
        {
            var tenant = id <= Contents ? Tenant : 2;
            var content = new ContentSeed(id, tenant, ArticleType, $"Article {id}", start.AddMinutes(id % 700))
            {
                HeadLine = $"Headline {id}",
                Abstract = $"Abstract of article {id}.",
                Description = $"<p>Description of article {id}.</p>",
                Metadata = new(id, $"Meta {id}"),
                Sections = Enumerable.Range(0, SectionsPerContent).Select(s => new SectionSeed(id * 10 + s, s,
                    Enumerable.Range(0, ElementsPerSection).Select(e => new ElementSeed((id * 10 + s) * 10 + e, e % 3,
                        $"Tiny text {id}.{s}.{e}", $"<p>Editor text for {id}.{s}.{e}, a paragraph of typical length.</p>", IsActive: e != 0)).ToList())).ToList(),
                Images = Enumerable.Range(0, ImagesPerContent).Select(i => new ImageSeed(id * 10 + i, $"{id}-{i}.jpg", 430 + i * 215)).ToList(),
                CategoryIds = [tenant == Tenant ? Category : 12],
                TagIds = id % 3 == 0 && tenant == Tenant ? [30] : []
            };
            db.Content(content);
            if (id % 2 == 0)
                db.Translation(id, content, FaIR, text => $"[fa] {text}", fingerprint: id % 10 == 0 ? "stale" : null);
        }
    });

    private static string Report(IEnumerable<string> rows)
    {
        var efVersion = typeof(DbContext).Assembly.GetName().Version;
        var packageVersion = typeof(DeliveryBaseline).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "ContentDeliveryVersion").Value;
        var report = new StringBuilder();
        report.AppendLine($"""
            # Content Delivery SDK - local performance baseline

            Non-gating. Regenerate with `src/Core/ContentDelivery.PackageTests/run.sh baseline` (packs the SDK,
            restores it into the fixture from the local feed, runs `DeliveryBaseline`). These are local
            SQLite in-memory timings of the packaged .NET 9 adapter: they compare runs of this benchmark
            with each other and are **not** SQL Server or production capacity figures (no network, no
            server, different query plans). Query counts and allocations transfer better than times.

            - Runtime: {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.RuntimeIdentifier}, {Environment.ProcessorCount} logical CPUs
            - Provider: EF Core {efVersion} with SQLite in-memory, substituted for SQL Server in the adapter's options (packages Cms.ContentDelivery[.SqlServer] {packageVersion})
            - Dataset: {Contents:N0} tenant contents (+{OtherTenantContents} of another tenant), each with metadata, {SectionsPerContent} sections x {ElementsPerSection} elements (1 inactive per section) and {ImagesPerContent} images; all in the listed category; 50% with a Ready fa-IR translation, 10% of those stale
            - Reads: document set of {ContentDeliveryLimits.MaxDocumentSetSize} scattered ids (the maximum); category + type listing, page 3 of size {ContentDeliveryLimits.MaxPageSize} (the maximum)
            - Method: one DI scope per read, as per web request; {Warmup} warmup reads, then {Iterations} measured reads, sequential. Allocation is process-wide bytes / read; queries are database commands / read. Cache disabled = every read hits the database; cache enabled = in-process cache after warmup, so every measured read is a hit

            | Read | Culture | Cache | Median ms | p95 ms | Allocated KB/read | Queries/read |
            |---|---|---|---:|---:|---:|---:|
            """);
        foreach (var row in rows)
            report.AppendLine(row);
        return report.ToString();
    }
}
