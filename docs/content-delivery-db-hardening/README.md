# Content Delivery database hardening (Phase 6)

**Status:** prepared for DBA review (2026-09-26). **Nothing here has been run against any SQL
Server.** Restored-backup validation is **pending DBA execution**; no isolation claim is made
until the evidence in section 5 is attached to the change ticket. Discovery §8 evidence
(`Content-Delivery-Discovery.md`) is also still pending; `01-preflight.sql` re-collects the
permission and RLS subset this phase depends on.

**Independent of the SDK release.** This is a database-only change. It needs no NuGet package
update, no SDK code change and no website code change: a website opts in by switching its CMS
connection string to the new least-privilege login. The released `Cms.ContentDelivery.SqlServer`
adapter keeps reading the same base tables; SQL Server filters the rows.

Scope: the 11 tables `ContentDeliveryDbContext` maps. Not changed: CMS schema or data, EF
configuration, BackOffice, translation workflows, public APIs, Diba or any other consumer.

## 1. Files

| Script | Runs as | Changes | Purpose |
|---|---|---|---|
| `01-preflight.sql` | DBA | nothing | Version, compatibility level, tables/columns, ownership, existing RLS, principals and permissions, target application, tenant spread. Counts only. |
| `02-create-website-login.sql` | DBA (securityadmin) | server login | Optional: SQL-auth login for the website, password from the secret store. |
| `03-deploy.sql` | DBA | additive objects | Security schema, mapping table, predicates, policy, website role and user. Idempotent, one transaction. |
| `04-verify-restored-backup.sql` | DBA, restored copy only | nothing (always rolls back) | Proves isolation as the website login with plain SQL, using a second tenant. |
| `05-verify-website-connection.sql` | the website login | nothing | Same checks over the website's own connection (no impersonation). |
| `06-rollback.sql` | DBA | removes hardening objects | Removes only what 02/03 created, dependency-safe. |
| `check_scripts.py` | anyone | nothing | Static consistency checks; proves nothing about RLS behaviour. |

All scripts are SQLCMD scripts (`sqlcmd -b`, or SSMS in SQLCMD mode). Every variable is passed with
`-v` (secrets through environment variables); no script sets defaults, so a missing value stops
the run.

## 2. Security design

- **Row-Level Security on the base tables.** One schema-bound policy
  `ContentDeliverySecurity.WebsiteTenantPolicy` adds a filter predicate to each adapter table:

  | Table | Visible to a website mapped to application A |
  |---|---|
  | `CMS_Contents`, `CMS_Categories`, `GNR_Tags` | `ApplicationId = A` |
  | `CMS_ContentMetadata`, `CMS_ContentSections`, `CMS_ContentImages`, `CMS_ContentTranslations` | owning content has `ApplicationId = A` |
  | `CMS_SectionElements` | owning section's content has `ApplicationId = A` |
  | `CMS_ContentInCategories`, `CMS_ContentInTags` | content **and** term both belong to A (rule T2) |
  | `GNR_Cultures` | `ApplicationId = 0` only (global); tenant-owned rows invisible, including A's own |

  Visibility (`IsActive`, `IsDeleted`, translation status) stays in the SDK; RLS enforces only
  the tenant boundary.
- **Identity, not caller input.** `fn_CallerScope` resolves the caller from `USER_NAME()` (the
  authenticated database user) against `ContentDeliverySecurity.WebsitePrincipalApplication`. No
  predicate reads `SESSION_CONTEXT`, `CONTEXT_INFO`, `APP_NAME()`, `HOST_NAME()`, connection
  string values or query parameters. The website cannot impersonate (no `IMPERSONATE`), change
  roles, alter the policy or read/write the mapping.
- **Fail closed for websites, unchanged for everyone else.** A caller is *restricted* when it has a
  mapping row or is a member of `ContentDeliveryWebsiteReader`; a role member without a mapping
  sees nothing. Every other principal (BackOffice, jobs, DBAs, `dbo`) is unrestricted, so existing
  behaviour is unchanged. The mapping, not role membership, is authoritative, so the
  `IS_ROLEMEMBER` domain-controller caveat can only hide rows, never expose them.
- **Ownership chain.** The security schema, the CMS tables and the functions are all owned by
  `dbo`; the preflight blocks deployment otherwise. Functions are inline, `WITH SCHEMABINDING`,
  two-part names, no type conversions, no recursion (child predicates join straight to
  `CMS_Contents`). The policy is `SCHEMABINDING = ON`, so predicate lookups need no caller
  permission while the website role is explicitly denied the whole security schema.
- **Least privilege.** The website user belongs only to `ContentDeliveryWebsiteReader`, which has
  column-level `SELECT` on exactly the columns the adapter maps (no translation provider/model/
  error/job columns, no `CreatedDT`/`Status`), and explicit `DENY` on writes, DDL, `EXECUTE`, the
  security schema, policy/principal changes and every other table, view and routine. No
  `db_owner`, `db_datareader`, schema-wide grant or direct grant to the user.
- **Privileged access stays separate.** BackOffice keeps its existing identity; DBAs use their own
  logins. Neither may be added to the role or the mapping, and neither is changed by this work.
  Only DBAs with `ALTER ANY SECURITY POLICY` / `db_owner` can change the policy or mapping.

### Known limits (accepted, documented)

- Objects created after deployment are not in the explicit `DENY` list. Re-run `03-deploy.sql`
  (idempotent) after any schema release, and keep `public` free of grants (preflight section 6).
- Schema binding blocks altering the predicate columns (`Id`, `ApplicationId`, `ContentId`,
  `SectionId`, `CategoryId`, `TagId`) while the policy exists. Other columns stay alterable.
- If the adapter later maps a new column, a website read fails with error 230 until the grant is
  extended. That is deliberate: re-run the checker, extend the grant, redeploy.
- RLS side channels (for example error-based inference with crafted predicates) are a documented
  SQL Server limitation. Website identities are used only by the SDK, and monitoring (section 7)
  watches for ad-hoc queries.
- Existing website principals (Diba's current connection) are **not** restricted until the
  website moves to the new login. This deployment does not touch them.

## 3. Required DBA inputs

| Input | Used by | Notes |
|---|---|---|
| `CmsDatabase` | all | Restored-copy database name for rehearsal, production name for deployment. |
| `WebsiteLoginName` | 01–04 | New, dedicated, individual login (SQL auth, gMSA/Windows user, or Entra user). Never a group, never shared with BackOffice. |
| `WebsiteUserName` | 01, 03 | New database user name for that login. |
| `WebsiteApplicationId` / `ExpectedApplicationId` | 01, 03, 04, 05 | The website's `GNR_Applications.Id`; must equal the website's `ContentDelivery:ApplicationId`. Kept in the change ticket, not in this repo. |
| `WebsiteLoginPassword` | 02 | Environment variable only, generated secret ≥ 24 characters without single quotes, stored in the secret store. |
| `SecondApplicationId`, `SeedTestRows` | 04 | See section 5. |
| Restored backup | 04 | Recent full backup of production restored to a non-production server. |

## 4. Preflight (read-only) — expected results

Run `01-preflight.sql` on the restored copy and again on production before deployment. Capture
all result sets.

| Check | Expected |
|---|---|
| 1 platform | `ProductMajorVersion` ≥ 13, database online and read-write. Compatibility level recorded (RLS needs no specific level). |
| 2 tables / columns | Every table `OK` (in `dbo`, owned by `dbo`); no missing mapped column; predicate columns `int`. |
| 3 existing policies / indexed views | No `BLOCKER` rows. |
| 4 hardening objects | All `OK: absent` on first deploy (or `INFO` when re-running). |
| 5 website login | `OK` (or `INFO` before 02 runs); no server role; not mapped to another user. |
| 6 permissions | Review every `REVIEW` row: `public`/`guest` grants reach every website identity. |
| 7 target application / spread / cultures | Application exists; `ActiveGlobalCultures` > 0. A `NOTE: single tenant` means isolation can only be proven with seeded rows. |
| 8 dependent modules | Informational: views/procs over these tables inherit the filter only for website identities. |

Any `BLOCKER` stops the rollout.

## 5. Verification — restored backup, then direct connection

**A single-tenant production copy is not evidence of isolation.** `04` must run on a restored
production backup with a second tenant, in one of these modes (all inside a transaction that is
always rolled back):

| Mode | Variables | Use when |
|---|---|---|
| Seed a temporary tenant (DBA-approved) | `SeedTestRows=1 SecondApplicationId=0` | Production has one tenant. Clones one application row, one content item with metadata, section, element, image and translation, one category and tag, own and cross-tenant relation rows, and tenant-owned cultures for both applications. |
| Existing second tenant | `SeedTestRows=0 SecondApplicationId=<id>` | The copy already has another tenant with content. |
| Existing tenant plus cross-tenant rows | `SeedTestRows=1 SecondApplicationId=<id>` | Also exercises relation rows that cross tenants. |

Seeding copies existing rows (all columns) and overrides only ownership columns, so it works
without knowing every NOT NULL column. If a clone fails (for example a unique index on a copied
column), the whole run rolls back with the error; the DBA adjusts and re-runs.

Steps:

1. `03-deploy.sql` on the restored copy with the canary website's inputs.
2. `04-verify-restored-backup.sql` (sysadmin, `ConfirmRestoredCopy=RESTORED_COPY`). It uses
   `EXECUTE AS LOGIN` for the real website login and two rolled-back probe users: one mapped to
   the second application, one role member with no mapping.
3. `05-verify-website-connection.sql` connected **as the website login**, twice (second run
   with a spoofed workstation name `-H`).
4. Rehearse `06-rollback.sql`, then `03-deploy.sql` again, on the copy. (`04`'s `cleanup` check
   already proves the verification left nothing behind.)

Expected results of `04`:

| Result | Expected |
|---|---|
| Per principal × table (`website`, `probe mapped to B`) | `LeakedRows = 0`, `MissingRows = 0`, `ForeignRows > 0`, `PASS` for all 11 tables. `INCONCLUSIVE` means nothing to hide in that table: seed or pick another tenant. |
| `probe unmapped role member` | `VisibleRows = 0` everywhere (`PASS`). |
| active global cultures readable | > 0 for mapped principals, 0 for the unmapped probe. |
| spoof: SESSION_CONTEXT / CONTEXT_INFO | foreign rows visible = 0, content count equals baseline. |
| write / DDL / mapping / predicate function / policy / role / impersonate / unrelated table / unmapped column probes | `error 229/230/262/15151/15247/15517…`, `PASS`. `REVIEW` means an unexpected error number: inspect before accepting. |
| sdk shape probes (`COUNT(*)`, `EXISTS`, filtered `TOP`) | `succeeded`, `PASS`. A failure here blocks the rollout (column-level grants and SDK query shapes disagree). |
| mapping SID matches; cleanup | `PASS`. |
| `OverallVerdict` | `PASS`. `INCONCLUSIVE` is not acceptable for sign-off. |

Expected results of `05`: every row `PASS`, identical on both runs.

**Evidence to attach to the change ticket:** the preflight output (restored and production), the
full `04` output (three result sets, including server, database, time and operator), both `05`
outputs, the rollback rehearsal output, and the backup's name and restore time. No content rows
are produced by any script.

## 6. Rollout runbook

1. **DBA review.** Review `03`/`06` line by line with section 2. Run `check_scripts.py`. Record the
   approved application ids and login names in the change ticket.
2. **Backup restore rehearsal.** Restore a recent production backup to a non-production server.
   Run `01`, `02` (if SQL auth), `03`, `04`, `05`, `06`, then `03` again. All section 5
   expectations must hold. Measure a representative BackOffice workload before and after `03`
   (the predicate adds a mapping lookup for every caller).
3. **Least-privilege website identity.** In production, create the login (`02`, or the DBA's
   gMSA/Entra process), store the secret, and run `01` again.
4. **Deploy.** Run `03-deploy.sql` in production in a change window. It is additive and does
   not affect any existing principal; the deployment summary must show 11 predicates.
5. **Direct-SQL verification in production.** Run `05` as the website login with the canary's
   application id. Do **not** run `04` in production.
6. **Canary application configuration.** Point one website's CMS connection string (the one
   passed to `AddSqlServerContentDelivery`) at the new login, through its secret store. Keep
   `ContentDelivery:ApplicationId` equal to the mapped id: a mismatch makes the SDK see no
   content (fail closed), not another tenant's content.
7. **SDK smoke reads.** Through the website: a document by id, a document set, a listing by
   category and by tag, taxonomy, sitemap, a localized read, and the `content-delivery-database`
   health check. Compare with the pre-switch responses.
8. **Monitoring** (first week, then routine): SDK errors and latency (`Cms.ContentDelivery`
   meter), SQL error 229/230 for the website login, failed logins, BackOffice query latency, and
   any `ALTER`/`DROP` on the security schema or policy (SQL Audit or default trace). Alert on
   website-login queries not issued by the SDK host.
9. **Rollback decision.** Roll the *website* back first: restore its previous connection string.
   Roll the *database* back (`06`) only if BackOffice is affected or the policy is wrong; see
   section 9. Repeat steps 3–7 per website afterwards.
10. **Credential rotation.** See section 8.

## 7. Adding websites and schema changes

Another website: new login and user, then `03-deploy.sql` with its inputs (existing objects are
reused), then `05` with its application id. `03` refuses to re-point an existing user to a
different application; do that through rollback of that user or a reviewed manual change.

After any CMS schema release: re-run `01` (blockers) and `03` (extends the explicit `DENY` list to
new objects).

## 8. Credential rotation

SQL-auth: generate a new secret, `ALTER LOGIN [<login>] WITH PASSWORD = N'<new>' OLD_PASSWORD =
N'<old>'` run by the DBA from the secret store, update the website's secret, restart or reload
the website, confirm the health check, then revoke the old secret. For zero downtime, create a
second login/user with `02`/`03` (same application id), switch the website to it, then remove
the first user with a reviewed `DROP USER` and `DROP LOGIN`. gMSA/Entra identities rotate through
their own platform. Rotate immediately if a website host is compromised; rotation needs no policy
change.

## 9. Rollback

1. Switch every hardened website back to its previous connection string.
2. `sqlcmd -b -i 06-rollback.sql -v CmsDatabase="<db>" ConfirmRollback=ROLLBACK`. It drops, in
   order: hardening-tagged users, the website role (with all its grants/denies), the policy, the
   predicate functions, the mapping table and the schema. It refuses to drop anything it did not
   create and never touches CMS tables or rows.
3. If `02` created the login and no other database uses it: `DROP LOGIN [<login>]` by the DBA,
   then retire its secret.
4. Run `01` to confirm section 4 shows the hardening objects absent.

## 10. Local validation

`python3 docs/content-delivery-db-hardening/check_scripts.py` checks, without SQL Server: the
policy covers exactly the adapter's tables; column grants equal `ContentDeliveryDbContext`'s
mapped columns; predicates are schema-bound and read no caller-controlled state; no forbidden
grants; preflight is read-only; `04` never commits; rollback drops every created object in a
dependency-safe order and touches no CMS data; no embedded credentials or connection strings.
Passing it says nothing about runtime isolation - only section 5 does.
