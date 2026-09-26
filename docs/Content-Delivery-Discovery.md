# Content Delivery SDK - Phase 0 Discovery and Contract Baseline

**Status:** discovery checkpoint (2026-09-26). Documentation only: no SDK behaviour, consumer,
schema, or database change. Companion to `Content-Delivery-SDK-Implementation-Plan.md` (§10,
Phase 0). SQL evidence is **pending DBA execution** of `content-delivery-discovery.sql`; nothing
below claims live-database confirmation.

## 1. Sources inspected

| Source | Scope |
|---|---|
| This repo, Core | `LocalizedContentReader` / `LocalizedContentReadRepository`, `ContentQueryRepository`, `ContentProvider`, `CategoryRepository`, `GetCategoriesHandler`, `ContentSourceFingerprint`, `BaseEntity`, `ContentTranslationConfiguration`, `ContentActivationRegressionTests` |
| This repo, public HTTP API | `Web/Areas/Api/{Content,Category,Slider}Controller.cs` (anonymous read routes) |
| This repo, SDK contract | `src/Core/ContentDelivery/*` (Phase 1, unchanged) |
| Existing docs | `schema-readiness.md`, `inspect-schema.sql`, `farsi-content-readiness-policy.md`, `Translation-Modernization-Plan.md` |
| Consumers | Sibling repositories whose `*.csproj` (depth ≤ 5) references Core assemblies. Only composition root, content gateways/controllers, and the views they feed were read. |

### Consumer inventory

| Consumer | How it reads CMS content | Inspected |
|---|---|---|
| **Diba** (`../diba`) | ASP.NET Core MVC, net9.0. References prebuilt `Application/Domain/Infrastructure/Services.dll` copies by `HintPath`; registers its own `ApplicationDbContext` on the CMS connection string; reads through Core `IContentProvider`, Core `ICategoryRepository`, and a local `ContentPageRepository` that queries EF directly. Razor partials inject the repository for child reads. | Yes |
| `../diba-expired` | Same `HintPath` reference pattern; last commit Dec 2025, predates Diba's current head. Treated as a superseded snapshot. | csproj only - owner to confirm it is not deployed |
| External HTTP clients of `api/Content/*`, `api/Category/*`, `api/{app}/GetSlider/*` | Not discoverable from local project metadata. | No - listed as unknown |

No other locally accessible project references Core assemblies. No consumer file was modified.

## 2. Public read shapes observed

| # | Shape | Core today | Diba today |
|---|---|---|---|
| R1 | **Full document** by id | `GetContentByIdFull` (tenant + `!IsDeleted`; **no `IsActive`** on content or descendants; sections by `Priority` only; elements unordered; no metadata). `LocalizedContentReader.Read` (tenant + `!IsDeleted`, sections `Priority,Id`, elements `Id`, images `Id`, metadata included; no `IsActive`). | Details page: local EF read by bare id - **no tenant filter**, no `IsActive`, elements unfiltered; metadata, images, sections, elements rendered. Falls back to a per-section child read from Razor when elements are not loaded. |
| R2 | **Document set** (page composition) | None. | Every landing/static page loads *all* contents of one type (no tenant, no `IsActive`), then picks a fixed set of ids into view-model slots. Largest observed composition is about a dozen documents. Partials issue one child element read per section (N+1). |
| R3 | **Listing** (paged summaries) | By type: fixed 15/page, `CreatedDT` desc. By category: 1-based (0 = alias for 1), size ≤ 200, join-row `CreatedDt` desc then join `Id` desc. By category + date range: fixed 15. Category "box": top 10. `ContentProvider` by category/tag: 0-based, `UpdatedDT` desc, no tie-breaker. All require content `IsActive && !IsDeleted` and an active, owned filter term. | Category listing via `ContentProvider` (0-based, size 10); first image of any size as the card image. |
| R4 | **Taxonomy / navigation** | `api/Category/GetCategories/{app}/{parent}`: tenant + `!IsDeleted`, **no `IsActive`**, `Id` order, filtered by `ParentId` (0 = root; no FK). | Header navigation is hard-coded; listing page reads a category and its parent by bare id (base `Repository.GetById`, no tenant) to title the page, 404 if either is missing. |
| R5 | **Sitemap** | None. | Iterates a hard-coded category list through the listing read (page size 200) for each culture; entry URL uses a slug of the English title plus id; `lastmod` = `UpdatedDT`; Farsi entry skipped when no legacy snapshot exists. |
| R6 | **Media** | Images are `ContentImages` rows (file name + size variant); element files/galleries are fields on `SectionElements`. Listings filter by size variant (640, or 430/640/860). | Storage base URL from site config plus a fixed path convention; element types switch rendering (tiny text, image, rich text, ...). |
| R7 | **Localization** | Canonical `CMS_ContentTranslations` (Ready + current fingerprint + JSON + structure match) → legacy `FarsiContent` (configured legacy culture only) → source, in `LocalizedContentReader`, behind rollout gates. Activation of content requires a Ready translation of the current source. | Deserializes raw `FarsiContent` into a `Content` entity per block/page; redirects to the culture root when a detail page has no Farsi snapshot; hides Farsi cards/sitemap entries without one. |
| R8 | Slider (`SCM_`) | `api/{app}/GetSlider/{sliderId}`. | Not used. |

## 3. Scenario fixtures for SDK contract tests

Generic, identifier-free scenarios distilled from Diba. Tests seed their own tenants, types,
categories, and ids; no consumer name, id, route, category, or view model belongs in SDK code.

| Id | Scenario | Expected SDK behaviour |
|---|---|---|
| F1 | Page composed of N configured documents, one culture | `GetDocumentSetAsync` returns them in requested order in one bounded operation; no follow-up reads. |
| F2 | Same composition where one id is missing, deleted, or owned by another tenant | That id is omitted; the rest are returned in order; result is `Found`. |
| F3 | Composition in the legacy culture where documents have mixed translation states | Each document reports its own `LocalizationInfo.Source` (`Translation`, `LegacyFarsi`, `Source`). |
| F4 | Details by id | Full document: metadata, images, sections (`Priority`, `Id`), elements (`Id`), categories, tags. |
| F5 | Views that address elements by position within a section | Element order is stable (`Id`) across calls and cultures. |
| F6 | Details id owned by another tenant, deleted, or non-positive | `NotFound`, indistinguishable from a missing id. |
| F7 | Details for inactive content | Per decision D1 (recommended `NotFound`). |
| F8 | Details requested in a culture with no current localized text | `Found` with `Source` resolution and `Culture` set; the website decides whether to redirect. |
| F9 | Malformed legacy snapshot or malformed canonical JSON | Source text served; never an exception, never partial overlay. |
| F10 | Culture value that is not a language tag | `InvalidCulture`, before any lookup. |
| F11 | Well-formed culture that is unknown, inactive, deleted, tenant-owned, or ambiguous | `InvalidCulture` (C2). |
| F12 | Category listing, page n of size 10, plus page beyond the end | 1-based; `TotalCount` real; beyond-end page is `Found` with no items. |
| F13 | Listing filtered by a category that is missing, inactive, or another tenant's | Per decision D5 (recommended `NotFound`). |
| F14 | Listing page heading needs the category and its parent's title | Served from `GetTaxonomyAsync` (tree via `ParentId`), no separate category read. |
| F15 | Active section containing a deleted element and an inactive element | Neither is delivered; a Ready translation of the current source still resolves (see V2). |
| F16 | Sitemap for all visible content with alternate cultures | One entry per visible content item; `Cultures` per decision D4. |
| F17 | Relation row linking content to another tenant's category/tag | Term never appears on the document, never matches a listing filter. |

## 4. Recommended public delivery rules

Each rule cites the existing Core behaviour it follows. Items marked **D#** need product-owner
approval (§5) before Phase 2 encodes them.

### Tenant

- **T1** Every query starts from `CMS_Contents.ApplicationId = <bound tenant>` (or the term's own
  `ApplicationId` for taxonomy). No public method accepts an application id. *(Phase 1 contract;
  every Core public read already filters by application.)*
- **T2** A relation (category, tag, translation) is honoured only when both sides belong to the
  tenant. Cultures are global rows, not tenant-owned (C2). *(`IsCategoryOwnedByApplication`, `ContentProvider` term checks.)*
- **T3** Cross-tenant ids behave exactly like missing ids. *(`GetLocalizedContent` 404.)*

### Visibility

- **V1** Delivered content is `!IsDeleted && IsActive` for **every** operation, including
  documents and sets. Listings already require this; `GetContentByIdFull`,
  `LocalizedContentReader`, and Diba's page/detail reads do not. `IsActive` is the documented
  publish toggle (`BaseEntity`) and activation already requires a Ready translation. **D1.**
- **V2** Deleted descendants are never delivered *(global soft-delete filter)*. Inactive sections,
  elements, and images are excluded from output *(Core `GetSectionElements`,
  `GetAllContentImages`; Diba's child element read)*. Localization is resolved against the full
  non-deleted master graph, because `ContentSourceFingerprint` includes section/element
  `IsActive`; filtering first would falsely mark current translations stale. **D2.**
- **V3** Metadata: deleted → `null`; its `IsActive` is ignored *(every current read ignores it)*.
- **V4** `PublishDt` is **not** a visibility gate *(no current read filters on it)*. **D3.**
- **V5** Categories/tags are delivered (taxonomy, document terms, listing filters) only when
  `IsActive && !IsDeleted` and owned by the tenant *(listing filter checks; taxonomy today omits
  `IsActive` - aligning it is part of D5)*.

### Culture

- **C1** `null`/empty culture → source text, `LocalizationInfo { Culture = null, Source = Source }`.
- **C2** Delivery cultures are active, non-deleted global rows (`Culture.ApplicationId = 0`).
  A malformed tag (`^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}\z` fails) → `InvalidCulture`, before any
  lookup. A well-formed tag must match exactly one such row, case-insensitively; the result
  reports that row's canonical key. Unknown, inactive, deleted, tenant-owned
  (`ApplicationId <> 0`) or ambiguous (duplicate key) → `InvalidCulture`. *(Shipped SDK
  behaviour; Core's `LocalizedContentReader` differs.)*
- **C3** Canonical translation served only when non-deleted, same culture, `Ready`, current
  source fingerprint, valid JSON, and exact master id structure. *(`LocalizedContentReader.Resolve`.)*
- **C4** Legacy `FarsiContent` only for the configured legacy culture while fallback is enabled,
  and only after it parses and rebases cleanly; otherwise source. Configuration is server-side,
  never per request (gap G4).
- **C5** The SDK never 404s for missing localized text; it reports the resolution. Redirect or
  hide policies stay in the website.
- **C6** Non-text fields (ids, layout, element types, files, galleries, images, sizes) always come
  from the master graph. *(`LocalizedContentReader.Project`.)*

### Not found and results

- **N1** Document: missing, deleted, inactive (D1), cross-tenant, or non-positive id → `NotFound`.
- **N2** Document set: unfound ids omitted; a set with no found ids is `Found` with an empty list.
- **N3** Listing whose filter term is not visible → `NotFound` **(D5)**; a visible term with no
  content → `Found`, empty page.
- **N4** Taxonomy and sitemap never return `NotFound`; empty collections instead.
- **N5** Caller bugs (null arguments, out-of-range page, oversize set) throw argument exceptions,
  consistent with `ContentListingQuery` validation; they are not statuses.

### Ordering

- **O1** Every order ends with `Id` as the final tie-breaker. Sections `Priority, Id`; elements
  `Id`; images `Id`; categories and tags `Id`; sitemap `Id`. *(`LocalizedContentReader`,
  `CategoryRepository.List`.)*
- **O2** Listing primary order: Core uses three different keys today (`UpdatedDT`, `CreatedDT`,
  join-row `CreatedDt`). Recommended `PublishDt desc, Id desc`, contingent on DBA evidence §8
  item 6b; otherwise `UpdatedDT desc, Id desc` (Diba's current behaviour). **D6.**
- **O3** Document set: requested order; duplicate ids returned once at first position.

### Pagination and limits

- **P1** 1-based `PageNumber`, default size 20, size 1..100 enforced at construction
  *(Phase 1 contract)*. `TotalCount` is counted at the database with the same predicate.
- **P2** Page beyond the end → `Found`, empty `Items`, real `TotalCount`.
- **P3** Core's 0-based, 0-alias, fixed-15, and ≤ 200 variants are compatibility behaviours of the
  existing HTTP/Core reads only; the SDK does not replicate them.
- **P4** Document set ≤ 50 ids (`ContentDeliveryLimits.MaxDocumentSetSize`); observed compositions
  need about a dozen.
- **P5** Sitemap is unpaged; one lightweight projection. Revisit only if measured size requires it.

### Media and HTML

- **M1** The SDK returns file names and size variants, not URLs. Storage host and path conventions
  are website configuration.
- **M2** `EditorText` and `GalleryImages` pass through unchanged; the SDK does not sanitize or
  parse them. Rendering stored HTML safely remains the website's responsibility.

## 5. Decisions requiring product-owner approval

| Id | Question | Recommendation | Why it is not silently decided |
|---|---|---|---|
| D1 | Must documents and sets require content `IsActive`? | Yes. | Changes current document reads; page blocks that are inactive but displayed today would disappear. Evidence: §8 item 6a. A temporary compatibility switch may be needed for migration. |
| D2 | Exclude inactive sections/elements/images from delivered documents? | Yes (V2). | `GetContentByIdFull` returns them today. |
| D3 | Should a future `PublishDt` hide content (scheduled publishing)? | No, for now. | Would be a new product behaviour; `PublishDt` data quality is unverified (6b). |
| D4 | Does `SitemapEntry.Cultures` list a culture when only source text would be served? | No - list only cultures resolving to `Translation` or `LegacyFarsi`. | Determines alternate-language SEO links; the source language needs a defined entry. |
| D5 | Listing by a missing/inactive/foreign term: `NotFound` or empty page? Taxonomy: exclude inactive terms and subtrees under hidden/missing parents? | `NotFound`; exclude. | Core deliberately returns empty today; taxonomy includes inactive terms today. |
| D6 | Listing primary sort key. | `PublishDt desc, Id desc` if 6b is clean. | Three conflicting current behaviours; visible to readers. |
| D7 | Is the "source language" for sitemap/alternate links a configured culture key? | Configure it with the tenant. | No culture row is marked as source today. |

## 6. Mapping to the Phase 1 contract

| Shape | Phase 1 operation | Fit |
|---|---|---|
| R1 Full document | `GetDocumentAsync` → `ContentDocument` | Covered (metadata, sections, elements, images, terms, localization, version). |
| R2 Document set | `GetDocumentSetAsync` | Covered; replaces "load whole type, pick ids" and view-time child reads. |
| R3 Listing | `GetListingAsync` (`TypeId`/`CategoryId`/`TagId`) | Covered except primary-image rule (G1), created date (G3), date range (G5). Top-N "box" = page 1 with a small size. |
| R4 Taxonomy | `GetTaxonomyAsync` | Covered; map `ParentId = 0` to `null`. |
| R5 Sitemap | `GetSitemapEntriesAsync` | Needs a title/slug source (G2) and D4/D7. |
| R6 Media | `MediaReference`, element `FileName`/`GalleryImages`/`Size` | Covered by M1. |
| R7 Localization | `culture` argument + `LocalizationInfo` | Covered; legacy configuration missing (G4). |
| R8 Slider | - | Not covered; no confirmed website consumer (G5). |

### Genuine contract gaps (to resolve in Phase 1 follow-up, not in this checkpoint)

- **G1** `ContentSummary.PrimaryImage` has no defined selection rule; consumers pick size variants
  (640; 430/640/860; or "first image"). Define a rule or expose summary image variants.
- **G2** `SitemapEntry` has no title or slug; consumers build detail URLs from the source title.
- **G3** No created date on summaries/documents; a consumer displays `CreatedDT`. `PublishedAt`
  may replace it if 6b shows `PublishDt` is reliable.
- **G4** `ContentDeliveryOptions` has only `ApplicationId`; Phase 3 needs legacy culture id and
  fallback switch (and D7's source culture), validated at startup.
- **G5** Date-range category listing, slider reads, and content attachments have no SDK operation;
  no locally confirmed website uses them. Deferred until a consumer is confirmed.

### Website presentation logic (stays out of the SDK)

Route shapes and slugs, slug-from-title generation, navigation menus and localized menu labels,
which documents fill which page slot, view models, element-type-to-HTML rendering and column
layout, storage base URL and path conventions, redirect-to-culture-root when localized text is
missing, static sitemap pages, and HTTP response caching.

### Consumer risks the SDK removes (observed, not changed)

Page and detail reads without a tenant filter; bare-id category reads; inactive content and
elements rendered; raw `FarsiContent` deserialized into entities (stale structure, exceptions on
malformed data); N+1 child reads from views; hard-coded tenant id in source; stale copied Core
binaries.

## 7. Tenant boundary and database posture

- **Boundary:** one website = one application id, bound once at startup and validated
  (`ContentDeliveryOptions`, `ContentDeliveryTenant`). All reads derive scope from it (T1-T3).
- **Application code is not the security boundary.** Target posture (plan §6): one SQL login per
  website; read-only; no direct table access; tenant enforced in the database (scoped
  views/procedures or RLS keyed to the principal); BackOffice on a separate privileged identity.
- **Current state is unverified.** Diba connects with its own EF context on a CMS connection
  string whose principal and permissions are unknown here. Whether any website principal can read
  other tenants, write, or alter schema has **not** been checked. Phase 6 hardening must not start
  until §8 evidence is returned.

## 8. DBA checklist (evidence pending)

Run read-only on production or a restored copy: first `inspect-schema.sql`, then
`content-delivery-discovery.sql`. Return results; no change is requested.

| # | Evidence | Script section | Status |
|---|---|---|---|
| 1 | Delivery tables present (incl. `CMS_ContentTranslations`) with columns, PKs, FKs, unique indexes | `inspect-schema.sql` §1-5; discovery §1 | Pending |
| 2 | All indexes on delivery tables; FK columns without a leading index | discovery §2 | Pending |
| 3 | Database users, mapped logins, role memberships | discovery §3a-3b | Pending |
| 4 | Explicit and effective permissions per user on delivery tables | discovery §3c-3d | Pending |
| 5 | Existing RLS policies/predicates; objects already reading CMS tables | discovery §4-5 | Pending |
| 6a | Active / inactive / deleted content per application and type (D1) | discovery §6a | Pending |
| 6b | `PublishDt` unset / future / after-update counts (D3, D6, G3) | discovery §6b | Pending |
| 6c | Inactive descendants under active content (D2) | discovery §6c | Pending |
| 6d | Cross-tenant category/tag relation rows (T2) | discovery §6d | Pending |
| 6e | Category hierarchy integrity (D5) | discovery §6e | Pending |
| 6f | Duplicate active global culture keys (C2) | discovery §6f | Pending |
| 6g | Translation status and JSON validity per culture; legacy coverage (C3, C4) | discovery §6g | Pending |
| 6h | Ordering-key ties (O1, O2) | discovery §6h | Pending |
| 7 | Which principal each website connection string uses | Manual (ops) | Pending |
