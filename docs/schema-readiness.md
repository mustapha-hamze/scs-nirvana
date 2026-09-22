# Core SQL Server schema readiness

**Version:** 1.0
**Scope:** the `Core` solution's EF model (`Infrastructure/Data/ApplicationDbContext.cs` and
`Infrastructure/Data/Configurations/**`) versus the live production SQL Server schema.
**Status:** no EF migration history exists in this repo. Production schema is externally managed
and was never generated from this EF model — everything below is what the *model* declares, not
a confirmed description of production. Run `inspect-schema.sql` (same folder) against the real
database to confirm or refute each claim before relying on it.

## 1. How to use this document

1. Run `docs/inspect-schema.sql` against production (or the closest pre-prod copy). It is
   read-only — SELECTs against `INFORMATION_SCHEMA`/`sys.*` only, nothing is created, altered, or
   dropped.
2. Compare its output section-by-section against §2–§5 below.
3. Where they disagree, production wins — update this document, not the other way around, unless
   the disagreement is itself the bug (e.g. a uniqueness constraint the EF model already expects
   but production doesn't have yet — see §4).
4. Never edit an `IEntityTypeConfiguration<T>` to *invent* a delete rule, index, or column based
   on what "seems right" — only encode what §1's script actually confirmed in production.

## 2. Table inventory

Every table the EF model expects, grouped by prefix (`GNR_` general, `CMS_` content management,
`AME_` access management, `SCM_` custom modules). All but the three `CMS_ContentIn*` join tables
derive from `BaseEntity` (`Id`, `Status`, `IsDeleted`, `IsActive`, `CreatedDT`, `UpdatedDT`) via
the shared `ConfigureAudit<T>` helper, and therefore also carry the global EF soft-delete query
filter added in the "Centralize Core entity lifecycle policy" change.

| Entity | Table | Notes |
|---|---|---|
| `Application` | `GNR_Applications` | Tenant root. Not itself scoped to another `ApplicationId`. |
| `SystemLog` | `GRN_SystemLogs` | **Table name typo already in the codebase** (`GRN_`, not `GNR_`) — left as-is; renaming without confirming production has the same typo would break the mapping. |
| `Tag` | `GNR_Tags` | |
| `Culture` | `GNR_Cultures` | Has `ApplicationId`, but application code treats culture as a global lookup (`CultureServices.List()` is unscoped) — see the P1 architecture report. |
| `UserInApplication` | `GNR_UserInApplications` | EF model has a unique index on `(UserId, ApplicationId)` — **not confirmed live in production**; see §4. |
| `UserAccess` | `GNR_UserAccesses` | EF model has a unique index on `(UserId, ApplicationId)` — **not confirmed live in production**; see §4. |
| `ApplicationSetting` | `GNR_ApplicationSettings` | |
| `SystemType` | `GNR_SystemTypes` | |
| `UserAttachment` | `GNR_UserAttachments` | |
| `Sector` | `AME_Sectors` | |
| `SectorEntity` | `AME_SectorEntities` | |
| `EntityAccess` | `AME_EntityAccesses` | |
| `Category` | `CMS_Categories` | `ParentId` (self-reference) is a plain `int` column with **no FK configured** in EF — hierarchy is application-enforced only. |
| `Comment` | `CMS_Comments` | Not exercised by any service in the current codebase. |
| `Content` | `CMS_Contents` | |
| `ContentImage` | `CMS_ContentImages` | |
| `ContentMetadata` | `CMS_ContentMetadata` | One-to-one with `Content`. |
| `ContentSection` | `CMS_ContentSections` | |
| `SectionElement` | `CMS_SectionElements` | |
| `Schema` | `CMS_Schema` | |
| `SchemaDetails` | `CMS_SchemaDetails` | |
| `ContentAttachment` | `CMS_ContentAttachments` | |
| `ContentAttachmentItem` | `CMS_ContentAttachmentItems` | |
| `ContentInCategory` | `CMS_ContentInCategories` | Pure join row — **not** `BaseEntity` (no `IsDeleted`/soft-delete concept, no global query filter). Unique on `(ContentId, CategoryId)`. |
| `ContentInTag` | `CMS_ContentInTags` | Same shape as above. Unique on `(ContentId, TagId)`. |
| `ContentInCulture` | `CMS_ContentInCultures` | Same shape as above. Unique on `(ContentId, CultureId)`. |
| `Slider` | `SCM_Sliders` | |
| `SliderItem` | `SCM_SliderItems` | |

## 3. Foreign keys and delete behavior

Every parent/child relationship in the EF model, and its configured `OnDelete` behavior:

| Child → Parent | FK column | Configured `OnDelete` |
|---|---|---|
| `ContentInCategory` → `Content`, `Category` | `ContentId`, `CategoryId` | **Explicit `Cascade`** |
| `ContentInTag` → `Content`, `Tag` | `ContentId`, `TagId` | **Explicit `Cascade`** |
| `ContentInCulture` → `Content`, `Culture` | `ContentId`, `CultureId` | **Explicit `Cascade`** |
| `Content` → `Application` | `ApplicationId` | Not set — EF convention default (`Cascade`, since the FK is required) |
| `ContentSection` → `Content` | `ContentId` | Not set — convention default (`Cascade`) |
| `SectionElement` → `ContentSection` | `SectionId` | Not set — convention default (`Cascade`) |
| `ContentImage` → `Content` | `ContentId` | Not set — convention default (`Cascade`) |
| `ContentMetadata` → `Content` (1:1) | `ContentId` | Not set — convention default (`Cascade`) |
| `ContentAttachment` → `Content` | `ContentId` | Not set — convention default (`Cascade`) |
| `ContentAttachmentItem` → `ContentAttachment` | `AttachmentId` | Not set — convention default (`Cascade`) |
| `SchemaDetails` → `Schema` | `SchemaId` | Not set — convention default (`Cascade`) |
| `SliderItem` → `Slider` | `SliderId` | Not set — convention default (`Cascade`) |
| `Sector`, `Culture`, `SystemLog`, `SystemType`, `Tag`, `ApplicationSetting` → `Application` | `ApplicationId` | Not set — convention default (`Cascade`) |
| `SectorEntity` → `Sector` | `SectorId` | Not set — convention default (`Cascade`) |
| `EntityAccess` → `SectorEntity` | `EntityId` | Not set — convention default (`Cascade`) |

**Only the three join-table configurations set `OnDelete` explicitly.** Every other relationship
relies on EF Core's convention default for a required (non-nullable) FK, which is `Cascade`.
Since the application itself never issues a real SQL `DELETE` for any `BaseEntity` row any more
(the lifecycle-policy change converts every delete into a soft delete, `IsDeleted = true`), these
cascade rules only matter for: (a) whatever hard-delete tooling/scripts exist *outside* this
codebase, and (b) confirming the assumption is even correct — **run §4 of
`inspect-schema.sql` and compare**; do not assume convention-default `Cascade` matches what
production actually has.

## 4. Required membership/access uniqueness constraints

Core P0 added these two indexes to the EF model (`UserInApplicationConfiguration`,
`UserAccessConfiguration`):

- `GNR_UserInApplications`: unique on `(UserId, ApplicationId)`
- `GNR_UserAccesses`: unique on `(UserId, ApplicationId)`

They exist in the EF model and are exercised by SQLite-backed tests (`ApplicationRepositoryTests`),
but **no migration has ever been generated or applied against production**. Until the additive
migration in §6 is applied, do not claim these constraints are enforced live — a duplicate
`(UserId, ApplicationId)` row can currently still be inserted directly against production SQL
Server (bypassing `AddUserToApplication`'s own idempotent restore-or-insert logic would be the
only realistic way that happens through the app, but the database itself won't reject it).

## 5. `SP_ContentsInCategory`

Called from `Infrastructure/CMSRepository/ContentsInCategoryQueryAdapter.cs` via Dapper:

```
EXEC SP_ContentsInCategory @P_CategoryId = <int>, @P_ApplicationId = <int>
```

Expected result shape: rows mapping onto `ContentDto` (`Application.Contracts.CMS.ContentDto`).

This procedure's *definition* lives in the production database only — it is not checked into
this repo and this task does not have access to a live SQL Server to inspect it. Two things are
explicitly unverified and should be confirmed with §6 of `inspect-schema.sql`:

1. **Does the procedure itself filter `IsDeleted`/`IsActive` on `CMS_Contents`?** Dapper/raw SQL
   bypasses EF Core's global soft-delete query filter entirely — that filter only exists inside
   the EF query pipeline. If the procedure doesn't filter internally, a soft-deleted `Content` row
   can leak through this one read path even though every other read in the app now excludes it by
   default. `ContentServices.GetContentsInCategory` only gates the *category* (active,
   not-deleted, owned by the caller's application) before calling this — it does not re-check
   each `Content` row the procedure returns.
2. **Its exact parameter types/precision** — assumed `int`/`int` from the calling code; confirm
   against `sys.parameters`.

## 6. Production baseline and future migration procedure

No `Migrations/` folder exists in this repo, and `ApplicationDbContext` has never had a migration
generated against it. Introducing EF migrations into an already-populated, externally-managed
database needs a **baseline** step before any new migration can be applied normally:

1. **Generate a baseline migration** locally, scoped against the current EF model as of this
   commit: `dotnet ef migrations add InitialBaseline --context ApplicationDbContext --project
   Core/Infrastructure --startup-project Web`. Review the generated `Up()` — it should describe
   the schema this document and `inspect-schema.sql` say already exists.
2. **Do not run it as a real migration against production.** Instead, mark it as already applied
   there: insert a matching row into `__EFMigrationsHistory` (`MigrationId`,
   `ProductVersion`) via a manual, reviewed script, so EF considers the baseline satisfied without
   ever executing its `Up()` against a database that already has that schema.
3. From that point on, every schema change is a normal **additive** migration (e.g. the two
   unique indexes in §4): `dotnet ef migrations add AddMembershipUniqueIndexes`, review the
   generated SQL with `dotnet ef migrations script --idempotent` (a DBA-reviewable, idempotent
   script — this is what actually gets run against production, not `dotnet ef database update`
   directly against a database this team doesn't have unmediated write access to), get it
   reviewed like any other production DB change, then apply it during a change window.
4. Until step 1–2 happens, this repo has **no** mechanism to apply the §4 indexes (or any other
   schema change) to production other than a DBA running hand-written, reviewed SQL directly —
   which is the only thing that should happen before the baseline exists.

## 7. Optimistic concurrency (not implemented)

`UpdatedDT` is not marked as a concurrency token, and no `rowversion`/`timestamp` column exists
on any table. This is deliberate: adding either changes the column's semantics in a way that must
match the live column's actual type/precision, which is unverified (§1). If optimistic
concurrency is needed later:

- Confirm `UpdatedDT`'s live SQL Server column type/precision first (`datetime` vs `datetime2`,
  and `datetime2`'s scale) via `inspect-schema.sql` §2 — `datetime`'s ~3ms rounding can cause two
  writes within the same tick to collide as a false concurrency conflict, or two genuinely
  different writes to round to the same value and miss a real conflict.
- Prefer adding a dedicated `rowversion` column (`ALTER TABLE ... ADD RowVersion rowversion
  NOT NULL`) over repurposing `UpdatedDT` as the concurrency token — SQL Server maintains it
  automatically and it can't collide the way an application-set `datetime` can.
- That's a schema-adding migration (§6), applied the same additive/reviewed way — plus an EF
  model change (`IsRowVersion()`) and an explicit, tested conflict-handling policy
  (`DbUpdateConcurrencyException` → reload-and-retry vs surface-to-caller) in whichever use case
  writes that entity. None of that exists today; do not assume it does.

## 8. SQLite vs SQL Server test coverage

SQLite-backed tests (`Core.Tests`, the default) verify EF query/mutation logic, the global
soft-delete filter, and lifecycle stamping — they do not, and cannot, verify anything in §3–§5
that depends on the *actual* production SQL Server schema or the stored procedure's real
definition. `Core.Tests/Integration/SqlServerIntegrationTests.cs` adds opt-in-only coverage for
exactly that gap (see its own header comment for how to enable it); it is skipped by default and
in CI unless a real SQL Server connection string is supplied.
