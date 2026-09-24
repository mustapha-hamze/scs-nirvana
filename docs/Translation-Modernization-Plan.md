## Translation Modernization Plan

This should be an additive, zero-data-loss migration. `FarsiContent` must remain intact until the new flow has run safely in production for an agreed retention period.

### Phase 0: Production Discovery

Before changing behavior:

1. Take and restore-test a production backup.
2. Run read-only profiling for `FarsiContent`:
   - null/empty count
   - valid JSON count
   - malformed JSON count
   - payload size distribution
   - active contents without Farsi
   - contents whose English structure differs from the stored Farsi snapshot
3. Define fallback policy:
   - Farsi `Ready` -> return Farsi
   - Farsi `Stale` / `Failed` -> return English, or block publication based on business rules
4. Fix the current activation defect first: existing valid Farsi must activate content.

**No data or schema changes in this phase.**

### Phase 1: Add Translation Records

Add a new additive translation model; do not replace `FarsiContent`.

Suggested fields:

- `ContentId`
- `CultureId` or ISO culture code such as `fa-IR`
- `Status`: `Draft`, `Queued`, `Translating`, `Ready`, `Stale`, `Failed`, `NeedsReview`
- `SourceFingerprint`
- localized-text JSON payload
- `Provider`, `Model`
- `TranslatedAt`, `Error`
- audit fields

Database constraints:

- unique `(ContentId, CultureId)`
- index for `(Status, CultureId)`
- foreign key to Content
- no destructive table or column change

Use a DBA-reviewed, additive SQL Server script after comparing the live schema with the EF model. Test it against a restored production backup first.

### Phase 2: Version and Staleness

Do not use `UpdatedDT` alone as translation validity.

Build a canonical source document from:

- content textual fields
- metadata textual fields
- sections and element IDs
- translatable element text
- layout/structure identifiers

Hash that document as `SourceFingerprint`.

When English content or its structure changes:

- compare the current fingerprint with the translation fingerprint
- mark translations `Stale`
- do not silently serve an outdated Farsi structure as current

The content layout, images, file references, IDs, and relations remain in the English/master graph. Only text is localized.

### Phase 3: Backfill Existing Farsi Data

Create a resumable batch backfill job.

For each existing `FarsiContent`:

- parse and validate it
- extract only localized text into the new translation payload
- preserve the original `FarsiContent` unchanged
- mark valid imported records as `Ready` or conservatively `Stale`
- mark malformed/ambiguous rows as `NeedsReview`
- never call OpenAI during backfill

Run in small, monitored batches with checkpoints, metrics, and retry-safe idempotency.

### Phase 4: Background Translation

Remove OpenAI work from the activation HTTP request.

New flow:

1. User requests translation.
2. Create one idempotent job for `(contentId, culture, sourceFingerprint)`.
3. Background worker translates only text fields plus stable IDs.
4. Validate JSON, IDs, text-field types, and HTML structure.
5. Store translation as `Ready`, or `Failed` with a safe error.
6. Activation checks translation status instead of waiting for OpenAI.

This prevents duplicate OpenAI charges, five-minute requests, race conditions, and unclear retry behavior.

### Phase 5: Dual Read and API Evolution

Keep existing API behavior initially.

- Existing callers still receive `FarsiContent`.
- New API contract accepts an explicit culture, such as `?culture=fa-IR`.
- New API returns a resolved localized content representation, not a raw language blob.
- During migration:
  1. prefer new `Ready` translation
  2. fall back to legacy `FarsiContent`
  3. fall back to English

Use feature flags per environment, then per application/tenant for controlled rollout.

### Phase 6: Manual Translation

Manual Farsi editing should update the new translation record, not clone and rewrite the complete content graph.

The editor should work with:

- source text
- translated text
- stable `ContentId` / `SectionId` / `ElementId`
- translation status and source version

If the English source changes during manual editing, reject or mark the save stale instead of silently overwriting newer structure.

### Phase 7: Rollout and Cleanup

1. Deploy additive schema and read support.
2. Backfill a small production subset.
3. Validate content rendering, activation, Farsi fallback, and performance.
4. Enable new writes for a pilot tenant.
5. Enable background translation.
6. Switch reads gradually.
7. Keep legacy `FarsiContent` for a defined retention window.
8. Only consider deprecation after verified migration coverage and rollback confidence.

Do not drop `FarsiContent` during this program. It remains the rollback safety net.

### Immediate Priorities

1. Fix activation when valid Farsi already exists.
2. Validate stored Farsi before activation.
3. Introduce source fingerprint and stale status.
4. Move OpenAI translation to an idempotent background workflow.
5. Add the new translation table and begin non-destructive backfill.