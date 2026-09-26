# Content Delivery SDK Consumer Handover

## Purpose

This guide is for every public website that receives CMS content. It replaces direct CMS database queries, copied `Application`/`Domain`/`Infrastructure`/`Services` DLLs, direct EF Core use, and manual `FarsiContent` deserialization with the in-process Content Delivery SDK.

The SDK is not an HTTP API. A website references two NuGet packages and reads the existing CMS SQL Server database in-process through a tenant-scoped, read-only client. This guide does not change a website's routes, controllers, views, or content identifiers by itself.

## Prerequisites

- The website targets `.NET 9`.
- The release owner has made matching approved versions of `Cms.ContentDelivery` and `Cms.ContentDelivery.SqlServer` available in the team's package source. The current preview is `1.0.0-preview.1`; use the approved release version for the migration.
- The CMS database has SQL Server compatibility level `130` or higher.
- The website has one fixed CMS application identity and a dedicated read-only database login. The database login must be provisioned by the DBA. Never use `sa`, `db_owner`, or a BackOffice credential from a public website.
- Before a production rollout using the dedicated identity, the DBA has completed the restored-backup verification in [content-delivery-db-hardening](./content-delivery-db-hardening/README.md).

## Installation

Configure the approved package source through the normal website package-management process, then install matching package versions:

```bash
dotnet add package Cms.ContentDelivery --version 1.0.0-preview.1
dotnet add package Cms.ContentDelivery.SqlServer --version 1.0.0-preview.1
```

Do not reference CMS projects directly and do not copy CMS DLLs into the website.

## Website Configuration

Each website binds exactly one application at startup. The browser and website routes never send an application ID to the SDK. A content ID, category ID, tag ID, or type ID is only a lookup input; the configured application scope is always applied by the SDK and, after the DBA hardening rollout, by SQL Server too.

```json
{
  "ConnectionStrings": {
    "Cms": "<dedicated read-only CMS connection>"
  },
  "ContentDelivery": {
    "ApplicationId": 12345,
    "LegacyFallbackEnabled": false,
    "LegacyFallbackCulture": "fa-IR",
    "CacheEnabled": false,
    "CacheTtl": "00:00:30"
  }
}
```

Replace `12345` with the website's own approved CMS application ID. `ApplicationId` must be positive. `LegacyFallbackEnabled` is off by default; enable it only during an approved migration, and only with the culture that legacy Farsi snapshots represent. Cache is also off by default. When enabled, `CacheTtl` must be between one second and five minutes. The connection string belongs in the website's secret/configuration system, never in source control.

## Registration

Register the SDK once during website startup:

```csharp
using Cms.ContentDelivery;

var cmsConnection = builder.Configuration.GetConnectionString("Cms")
    ?? throw new InvalidOperationException("CMS connection is required.");

builder.Services.AddSqlServerContentDelivery(builder.Configuration, cmsConnection);
builder.Services.AddHealthChecks().AddContentDeliveryHealthChecks();
```

Health-check registration does not add an HTTP endpoint. The website decides whether and where to expose a protected health endpoint.

## Reading Content

Inject `IContentDeliveryClient` into the website service or controller that owns the read. The SDK returns delivery DTOs, never tracked EF entities.

```csharp
public sealed class PageContentService(IContentDeliveryClient content)
{
    public async Task<ContentDocument?> GetPageAsync(int contentId, string? culture,
        CancellationToken cancellationToken)
    {
        var result = await content.GetDocumentAsync(contentId, culture, cancellationToken);
        return result.IsFound ? result.Value : null;
    }
}
```

Map existing website reads to the generic operations:

| Website need | SDK operation |
| --- | --- |
| One details page or CMS page block | `GetDocumentAsync` |
| Ordered page composition from several content IDs | `GetDocumentSetAsync` |
| Type, category, or tag listing | `GetListingAsync` |
| Navigation, filters, category and tag labels | `GetTaxonomyAsync` |
| Sitemap generation | `GetSitemapEntriesAsync` |

`GetDocumentSetAsync` preserves the requested order, removes duplicates after their first use, and omits unavailable IDs. It accepts at most 50 IDs. Listings are 1-based, support page sizes from 1 to 100, and combine type/category/tag filters with AND.

A `ContentDocument` already contains ordered sections, elements, media, categories, and tags. A view must not query the CMS database again for every section or element.

## Result Handling

Document, document-set, and listing reads return `ContentDeliveryResult<T>`:

- `Found`: use `Value` only after `IsFound` or `TryGetValue(...)` confirms it is present.
- `NotFound`: use the website's normal missing-content behavior. This also covers content owned by another application, inactive content, and deleted content.
- `InvalidCulture`: reject or normalize the website's culture input. Do not treat it as missing content.

Taxonomy and sitemap reads return empty collections when there is no visible data. The website owns route policy, redirects, view models, and HTTP status choices.

## Localization

Pass `null` or an empty culture for the master/source text. A requested culture must be an active, non-deleted global CMS culture key, such as `fa-IR`; matching is case-insensitive.

For a valid requested culture, the SDK resolves text in this order:

1. A current, structurally valid canonical `Ready` translation.
2. An eligible legacy Farsi snapshot only when legacy fallback is explicitly enabled for that culture.
3. English/master source text.

Read `document.Summary.Localization` to see the canonical culture key and whether text came from `Translation`, `LegacyFarsi`, or `Source`. Do not read `FarsiContent`, translation JSON, fingerprints, provider/model values, errors, or jobs from a website. Layout, IDs, media, files, galleries, element types, and taxonomy always come from the master graph.

Sitemap `Cultures` contains only cultures with a current canonical translation or eligible legacy translation. Source-only fallbacks are not advertised as alternate-language links.

## Cache, Metrics, and Health

When caching is enabled, it is in-process only and uses the configured short TTL. There is no write-side cache invalidation; content can be up to that TTL old. Keep cache disabled during the first functional migration, then enable it after parity testing.

The SDK emits the `Cms.ContentDelivery` meter. It records read duration, cache use, localization resolution, and safe fallback outcomes. Metric tags do not contain content IDs, tenant IDs, text, translation payloads, or provider errors.

The optional health checks are `content-delivery-configuration` and `content-delivery-database`, tagged `content-delivery`. They validate startup configuration and database reachability without loading CMS content.

## Migration Procedure

1. Inventory one website feature's direct CMS reads and identify its matching SDK operation.
2. Add the packages, fixed application configuration, and dedicated read-only database connection in a non-production environment.
3. Replace only that feature's data-access service. Keep its route, controller, and view model stable where practical.
4. Verify source and `fa-IR` behavior against the current website output. Compare page structure, visible content, lists, media, navigation, and sitemap entries.
5. Enable the feature for a small production path or page group. Keep the previous read path available until the validation window closes.
6. Repeat feature by feature. Remove copied CMS DLLs, direct `ApplicationDbContext` use, raw SQL content reads, and direct `FarsiContent` parsing only after all website features use the SDK.

Do not add website-specific content IDs, type IDs, routes, or view models to the SDK.

## Required Website Tests

- Configured application returns its own visible content.
- Another application's content ID behaves as not found.
- Inactive or deleted content and descendants are not delivered.
- One document, ordered document set, filtered listing, taxonomy, and sitemap each render through the website's existing presentation layer.
- Valid canonical Farsi translation is served; stale, missing, malformed, or wrong-culture translation falls back safely to source.
- Legacy fallback is tested both disabled and enabled for its configured culture.
- Invalid and inactive culture requests are handled as `InvalidCulture`.
- Cached and uncached reads preserve the same content result.
- The website uses only the two delivery packages and has no direct CMS entity/DbContext reads.
- The dedicated website database principal cannot read another application's data by direct SQL; this is jointly verified with the DBA.

## Rollout and Rollback

Deploy one website feature at a time. Monitor page errors, missing-content rates, invalid-culture results, cache use, and localization resolution. The website may roll back a feature to its prior code path during the migration window, but it must not independently disable Row-Level Security or reuse an elevated database credential. Database-identity or RLS rollback is a separate DBA decision using the reviewed hardening rollback procedure.

The SDK is read-only: it never creates content, queues translations, calls OpenAI, or changes CMS records. Package rollback is therefore a website deployment decision and does not require a CMS data migration.

## Support Boundaries

Website teams own their website-specific mapping, routes, views, tests, and rollout. The CMS team owns SDK contracts, package releases, translation lifecycle, and database-hardening scripts. Any new generic delivery need should be proposed to the CMS team before a website adds direct database access again.
