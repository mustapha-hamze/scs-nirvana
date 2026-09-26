# Content Delivery SDK Implementation Plan

## 1. Purpose and Scope

Build a reusable, in-process Content Delivery SDK for every public website backed by this CMS.
The SDK replaces direct CMS database reads, direct EF Core usage, copied Core DLLs, and direct
`FarsiContent` deserialization in consuming websites. It is not an HTTP API and does not add a
network hop.

The SDK is implemented only in this repository. No consuming website, including Diba, is changed
by this work. Diba is a read-only case study used to identify delivery shapes and later validate
the SDK contract. A separate handover document and separate website work will follow after the
SDK is released.

## 2. Non-Goals

- Do not change a website's controllers, views, routes, configuration, or database queries.
- Do not add Diba-specific pages, content IDs, type IDs, categories, routes, or view models to
  the SDK.
- Do not expose `ApplicationDbContext`, EF entities, repositories, write services, translation
  jobs, providers, or raw `FarsiContent` to SDK consumers.
- Do not replace the BackOffice or its existing public API routes.
- Do not make destructive schema changes or remove legacy translation data.

## 3. Design Principles

1. **Tenant binding is internal.** A website has one immutable configured application identity.
   SDK callers never supply an `applicationId` per request.
2. **Delivery models are read-only DTOs.** The SDK returns explicit contracts, never Domain or EF
   entities.
3. **A content document is complete.** A full-content read contains ordered sections, elements,
   metadata, media, and localized text. Views must not issue child queries while rendering.
4. **Localization is resolved once.** The SDK returns canonical `Ready` translation when current,
   then the controlled legacy fallback during migration, then source text. It never returns raw
   language blobs.
5. **Every delivery read is tenant, state, and culture aware.** Content and tenant-owned terms
   are scoped to the website application; culture resolution follows the established global-culture
   model. Missing, deleted, inactive, cross-tenant, malformed, stale, or unsuitable translation
   data must not escape as current public content.
6. **The Core schema remains private.** The SDK is the supported delivery boundary; consumers do
   not depend on table names, navigation properties, or internal query implementation.

## 4. Consumer Evidence

The SDK must support all general delivery patterns currently observed in consuming websites:

- complete dynamic content documents for pages and reusable CMS blocks;
- several content documents loaded as one page composition;
- paginated content summaries filtered by content type, category, or tag;
- details pages addressed by a content identifier;
- category/tag/navigation data and sitemap generation;
- localized English/Farsi reads with media and non-text structure preserved from the master graph.

These are generic delivery capabilities. They are not page-specific SDK methods. Diba's current
direct graph reads, child reads from Razor, legacy Farsi JSON reads, category lists, and sitemap
are compatibility scenarios for contract and performance tests only.

## 5. Target Architecture

```text
Website MVC application
  -> CMS Content Delivery SDK public contract
      -> tenant-bound delivery use cases
          -> SQL Server read implementation
              -> CMS database using a website-specific read-only identity

BackOffice
  -> existing Core Application and Infrastructure write/read workflows
              -> CMS database using its privileged identity
```

The public SDK surface is separated from its SQL Server adapter:

```text
Cms.ContentDelivery
  public delivery DTOs, query objects, result/status types, configuration contracts, tenant context

Cms.ContentDelivery.SqlServer
  internal EF Core/SQL projections, DI registration, read-only database adapter
```

The final distribution may be one or more versioned internal NuGet packages, but the public
contract must remain independent of EF Core and the current Core implementation details.

## 6. Tenant and Database Boundary

Each website will eventually configure one application identity on the server:

```json
"ContentDelivery": {
  "ApplicationId": 1111
}
```

The SDK validates this configuration at startup and binds it to its internal tenant context. Content,
categories, tags, media, sections, elements, and translations are resolved from the scoped
content/application root. No public method accepts an `applicationId` argument.

The existing database stores active culture rows globally (`Culture.ApplicationId = 0`), including
`en-US` and `fa-IR`. Culture is therefore not a tenant-owned query in the current model. Phase 3
uses active, non-deleted global culture keys; any future tenant-specific culture override requires
an explicit precedence rule and a separate schema/contract decision.

Application code alone is not a complete security boundary. The production deployment target is:

1. one SQL login per website;
2. read-only permissions for each website login;
3. no direct table access for website logins;
4. tenant-scoped views/stored procedures or SQL Server Row-Level Security that derives the allowed
   application from the database principal;
5. a separate privileged identity for BackOffice.

This database work requires DBA review, restored-production-backup testing, and additive scripts.
It is not an EF migration task.

## 7. Public Delivery Contract

Define generic operations around delivery needs, not individual website pages:

| Operation            | Result                                | Use                                       |
| -------------------- | ------------------------------------- | ----------------------------------------- |
| Get content document | `ContentDocument`                     | Details page or one CMS block             |
| Get content set      | ordered `ContentDocument` collection  | One page composed from several CMS blocks |
| Get content listing  | paginated `ContentSummary` collection | Type/category/tag listing                 |
| Get taxonomy         | categories/tags/navigation DTOs       | Menus and filters                         |
| Get sitemap entries  | lightweight route entries             | Sitemap generation                        |

Identifiers supplied by website routes or configuration may be content IDs, category IDs, tag IDs,
or a future public key. They are lookup inputs only: the SDK applies its bound tenant scope before
returning a result. A bare section or element lookup is deliberately excluded.

Use two result shapes:

- `ContentSummary`: identity, canonical route information, localized summary text, publication
  data, primary media reference, and delivery version.
- `ContentDocument`: `ContentSummary` plus metadata, ordered sections, ordered elements, media,
  files/galleries, categories, tags, and resolved localization metadata.

`ContentDocument` keeps layout, IDs, images, file references, gallery values, element types, and
other non-text fields from the English/master graph. Only supported text fields are localized.

## 8. Localization and Publication Rules

The SDK extends the existing Core localized read work; it does not create a second translation
pipeline. Phase 2 serves master/source text only. The following resolution applies in Phase 3 and
later.

For a valid requested culture, resolve text in this order:

1. non-deleted canonical translation with `Ready` status, current source fingerprint, valid JSON,
   and an exact master-structure match;
2. valid legacy `FarsiContent` only when the requested culture is explicitly configured as the
   legacy culture and legacy fallback remains enabled;
3. source/master English text.

The delivery result reports the resolved culture and resolution source (`translation`,
`legacy-farsi`, or `source`). It does not disclose provider/model/error/job data. Stale, failed,
deleted, malformed, wrong-culture, or structurally invalid translations are never served as
current.

Define and test one explicit public-content visibility policy for active/published content and
active descendants. Compatibility mode may be needed while consumers are migrated, but the final
policy must be the SDK's documented behavior rather than individual website behavior.

## 9. Query and Performance Rules

- Use no-tracking, projection-first queries; load only the shape required by a summary or document.
- A document query must return the required child graph in one bounded delivery operation.
- Batch document reads must avoid one query per content item.
- Order sections and elements deterministically.
- Paginate at the database for listings; enforce bounded page sizes.
- Cache only delivery DTOs, never tracked entities or raw translation rows.
- Every cache key includes SDK version/schema version, application identity, culture, query shape,
  and content/version data. Cache entries must never cross tenants or cultures.
- Start with safe expiry and metrics. Add explicit invalidation from CMS content-change events only
  after the initial delivery contract is stable.

## 10. Implementation Phases

### Phase 0: Discovery and Contract Baseline

1. Inspect relevant graph information and Core delivery/localization symbols only.
2. Inventory existing public read shapes across consuming applications without editing them.
3. Define the public visibility, culture, error/not-found, ordering, and pagination rules.
4. Record Diba scenarios as contract fixtures, without introducing Diba code or identifiers into
   the SDK.
5. Confirm live SQL schema and permissions with the DBA before any database-bound hardening work.

### Phase 1: Public Contracts and Tenant Context

1. Add delivery abstractions, immutable DTOs, query objects, and result/status types.
2. Add validated, immutable website tenant configuration and internal tenant context.
3. Ensure public contracts do not reference EF Core, Domain entities, Infrastructure, BackOffice,
   or the legacy `FarsiContent` field.
4. Add contract and architecture tests for the public boundary.

### Phase 2: Scoped SQL Read Adapter

1. Implement tenant-scoped projections for documents, document sets, listings, taxonomy, and
   sitemap entries.
2. Apply deletion, activity/publication, ownership-chain, deterministic ordering, and pagination
   rules in the query itself.
3. Serve the master/source graph only. Validate only malformed culture-tag input; do not read
   translations, legacy `FarsiContent`, translation jobs, or fallback settings in this phase.
4. Add cross-tenant, deleted-descendant, malformed-data, source-only, and query-shape regression
   tests.

### Phase 3: Localized Delivery Resolution

1. Integrate culture validation against the existing active, non-deleted global culture rows
   (`Culture.ApplicationId = 0`).
2. Apply canonical translation, controlled legacy fallback, and source fallback consistently to
   document, batch, listing, and sitemap reads.
3. Return localization resolution metadata without internal translation details.
4. Add stale/fingerprint/structure/culture isolation tests.

### Phase 4: Caching, Observability, and Resilience

1. Add an optional cache abstraction and a conservative default policy.
2. Add structured metrics for query duration, cache use, resolution source, missing content, and
   invalid localization data; never log content payloads or sensitive identifiers beyond approved
   operational fields.
3. Define health checks for database availability and SDK configuration validity.
4. Add cache-isolation, cache-expiry, and no-write-on-read tests.

### Phase 5: Packaging and Compatibility Validation

1. Add versioned package build and dependency-boundary checks.
2. Verify consumers need only the delivery packages, not copied `Application`, `Domain`,
   `Infrastructure`, or deprecated `Services` binaries.
3. Run contract fixtures based on real consumer scenarios, including Diba's block, listing,
   detail, localization, and sitemap shapes.
4. Produce performance baselines for batch documents and paginated listings.

### Phase 6: Database Access Hardening

1. Produce DBA-reviewed, additive SQL scripts and deployment instructions for dedicated
   website read identities and tenant enforcement.
2. Test against a restored production backup before production rollout.
3. Validate that a website database principal cannot retrieve another application's content even
   when bypassing the SDK.
4. Keep this phase separately deployable from the SDK package release.

### Phase 7: Consumer Handover

After the SDK is complete, stable, and released, create a separate handover document for
websites team. It will cover package installation, configuration, migration of direct reads,
localization behavior, test cases, rollout, and rollback. Implementing those website changes is
outside this SDK program.

## 11. Acceptance Criteria

The SDK is ready for consumer adoption only when:

1. all public delivery reads are internally tenant-bound;
2. no public SDK contract exposes EF entities, raw JSON translations, or Core persistence types;
3. full documents eliminate the need for view-time section/element queries;
4. summaries and documents have tested, deterministic ordering and pagination;
5. localized delivery observes the current canonical translation safety rules;
6. results are read-only and no delivery request writes, queues, or calls OpenAI;
7. tenant/culture/cache isolation tests pass;
8. package dependency boundaries are enforced;
9. DBA deployment prerequisites and database-security validation are documented;
10. a consumer handover document can be written without requiring undocumented Core knowledge.

## 12. Data Safety

This program is additive and read-focused. It does not remove or rewrite existing content,
`FarsiContent`, translation rows, content relations, or website data. Any future public keys,
database-security objects, or cache-invalidation records require separate additive schema plans,
DBA review, restored-backup validation, and an explicit rollback procedure.
