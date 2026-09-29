# Legacy Farsi Translation Queue Implementation Plan

## Purpose

Create a SuperAdmin-only BackOffice page for finding content that has a legacy
`Content.FarsiContent` payload but no canonical translation record, selecting a
controlled batch, and queueing the selected content for background translation.

The feature translates the current English/master source through the existing
translation provider and creates canonical `ContentTranslation` records. It does
not migrate legacy Farsi text into the new format. The existing Phase 3 backfill
is the correct path when preserving existing Farsi text without an OpenAI call is
the goal.

## Scope

The new page will:

- filter eligible content by a configured type allow-list;
- allow filtering by one allowed type, title, and content ID;
- allow a SuperAdmin to select a bounded set of eligible content;
- queue translation using the existing idempotent job workflow;
- show live, content-by-content job progress without refreshing the page; and
- preserve the legacy `FarsiContent` bytes unchanged.

It will not:

- translate within an HTTP request;
- add a second worker, job engine, or provider integration;
- expose legacy Farsi JSON, prompts, source fingerprints, or provider errors to
  the browser;
- overwrite stale, failed, needs-review, or deleted canonical translations; or
- make the page available to ordinary content editors.

## Authorization And Tenant Boundary

Every route in this feature is SuperAdmin-only:

- the page;
- the candidate-list endpoint;
- the bulk-queue POST; and
- the batch-progress endpoint.

Server-side authorization is required on every endpoint. Hiding the menu entry
is only a usability detail and is not an authorization control.

SuperAdmin access does not remove tenant isolation. All reads, selected IDs, job
status checks, and queue requests remain scoped to the application currently
selected in BackOffice. A content ID from another application must behave as
missing and must never be returned in progress data.

The existing `Content.ChangeActivity` key remains the permission for the
ordinary, single-content translation action. It must not grant access to this
bulk page.

## Eligibility Rules

A candidate is content that meets all of the following conditions:

1. It belongs to the current application and is not soft-deleted.
2. Its `TypeId` appears in the configured allow-list.
3. Its legacy `FarsiContent` value is non-null and non-blank.
4. It has no `ContentTranslation` row for the configured target culture,
   including a soft-deleted row.
5. It has no queued or processing translation job for its current source
   fingerprint and target culture.

Both active and inactive content can be candidates. Translation may be prepared
before activation.

A soft-deleted translation row is deliberately excluded rather than treated as
missing. The current workflow never resurrects deliberately deleted translations.
Likewise, `Ready`, `Stale`, `Failed`, and `NeedsReview` translations are outside
this page's purpose; they already exist in the new system and need their own
explicit retry or review workflow.

The legacy payload does not need to be parsed for eligibility. It is used only
to identify content with old Farsi data. The worker translates the current master
source and never sends the legacy payload to the provider.

## Configuration

Add these settings beneath the existing `ContentTranslation` section:

```json
"ContentTranslation": {
  "LegacyBulkCandidateTypeIds": [
    1112, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008,
    1009, 10010, 10011, 10012, 10013, 10014, 10015, 10016, 10017
  ],
  "BulkRequestMaxItems": 25
}
```

Rules:

- Type IDs must be distinct, positive values.
- `BulkRequestMaxItems` must be positive and conservatively bounded. Start at
  25, then adjust only after observing provider cost and worker throughput.
- The target culture is resolved only from the existing
  `ContentTranslation:ActivationCultureId`; JavaScript must not supply a culture
  ID.
- The page is unavailable for queueing when the target culture is not configured
  or is no longer available.

## User Experience

Create a separate **Legacy Farsi Translation Queue** page under the BackOffice
Content area. Do not add it to the per-type `ContentList` screen.

The page contains:

- an allowed-type filter with an `All configured types` option;
- title/content-ID search;
- server-paged results;
- columns for content ID, title, type, active state, updated date, and queue
  status;
- a checkbox only for content that is eligible at render time;
- a selected-item count;
- a queue button that is disabled for an empty selection; and
- an explicit confirmation stating how many provider translation requests will
  be queued.

Queued and processing rows remain visible with their current state but cannot be
selected again. The browser maintains selection only for the current batch; it
does not provide a dangerous, unbounded "select every result" operation.

After submission, show a progress panel with total, queued, processing,
succeeded, failed, superseded, skipped, and a per-content status list.

Progress percent is:

```text
(succeeded + failed + superseded + skipped) / submitted item count * 100
```

There is no honest provider-side percentage for one OpenAI call. The progress bar
therefore represents completed content items, which is accurate and actionable.

## Application And Persistence Design

Add a focused application use case and repository port for:

- reading paged legacy-translation candidates;
- validating and submitting a bounded set of selected IDs; and
- reading safe progress snapshots for a submitted set of jobs.

Keep `ContentTranslationRequests.Request` as the only operation that creates or
requeues an individual translation job. The bulk use case must reuse its
idempotency and source-fingerprint behavior instead of duplicating job logic.

Before queueing each selected ID, the server re-checks the candidate rules. The
client's checkbox list is never trusted as authorization or eligibility evidence.
The submission response contains a safe result per requested content ID:

- `Queued`
- `AlreadyQueued`
- `AlreadyReady`
- `Skipped`
- `CultureUnavailable`
- `NotFound`

No new batch table is needed initially. The existing durable
`CMS_ContentTranslationJobs` rows already contain the state required for progress
and remain valid after a browser reload. A batch table should be added only if a
later requirement needs durable named batches, history, cancellation, or audit
ownership beyond the individual job records.

## Live Progress

Use authenticated HTTP polling initially, not SignalR. The current application
has no SignalR infrastructure, while job state is already durable in SQL Server.

After a successful bulk submission:

1. The browser retains the accepted content/job IDs.
2. It polls a SuperAdmin-only progress endpoint every two to three seconds.
3. The endpoint verifies current-application ownership before returning each
   safe state, attempt count, terminal state, and fixed error code.
4. The UI updates row states, aggregate counts, and the progress bar in place.
5. Polling stops when every accepted item is terminal, or when the page is left.

The worker continues to own all transitions:

```text
Queued -> Processing -> Succeeded | Failed | Superseded
```

When a source changes during a provider call, the existing worker marks the job
`Superseded`; it must never save a translation for the newer source from an older
request.

## Operational Prerequisites

Before enabling the page in an environment, confirm:

1. `CMS_ContentTranslationJobs` exists and matches the current application model.
2. `ContentTranslation:WorkerEnabled` is enabled.
3. `ContentTranslation:ActivationCultureId` references the active Farsi culture.
4. The provider configuration is available to the background worker.
5. The worker is healthy and can claim jobs.

The page should clearly report an unavailable queue configuration and refuse
submission when these requirements are not met. It must not fail only after a
user has selected a batch.

## Validation

Add focused tests at the application, persistence, and Web levels.

### Candidate Query

- current-application isolation;
- every configured type and type filtering;
- title and ID filtering, sorting, and paging;
- non-empty legacy Farsi requirement;
- exclusion of canonical translation rows in every state, including deleted;
- queued/processing job visibility and non-selectability; and
- no database writes during reads.

### Bulk Submission

- SuperAdmin-only access on all routes;
- rejection of a non-SuperAdmin with `Content.ChangeActivity` alone;
- deduplication and maximum-item enforcement;
- server revalidation of modified, deleted, foreign, or no-longer-eligible IDs;
- culture-unavailable behavior;
- repeated and concurrent submissions do not create duplicate jobs or duplicate
  provider work; and
- legacy `FarsiContent` remains byte-for-byte unchanged.

### Progress And Worker

- progress data is tenant-scoped and contains no payload or provider text;
- queued, processing, succeeded, failed, and superseded states update aggregate
  counts correctly;
- polling stops after terminal completion; and
- processing a selected batch produces current `Ready` translations through the
  existing worker behavior.

## Rollout

1. Deploy the feature with the page disabled or inaccessible until worker and
   culture prerequisites are verified.
2. Run a small SuperAdmin batch, such as 10 items, in a non-production-like
   environment first.
3. Verify canonical `Ready` translations, legacy-byte preservation, progress
   reporting, and provider charges.
4. Begin production batches at the configured maximum and monitor failures or
   superseded jobs before increasing that maximum.

## Implementation Phases

### Phase 0: Confirm Scope And Prerequisites

Confirm that the feature retranslates the current English/master source through
the provider rather than backfilling old Farsi text. Verify that the translation
job table, target Farsi culture, worker, and provider configuration are ready.
Configure the allowed content types and a conservative maximum batch size.

### Phase 1: Candidate Discovery

Implement the tenant-scoped, read-only candidate query. It finds non-deleted
content with an allowed type, non-empty legacy Farsi, no canonical translation
for the target culture, and no active job for the current fingerprint. Add
server-side type, title, ID, sorting, and paging filters.

### Phase 2: SuperAdmin Security

Add the separate BackOffice queue page and its endpoints. Restrict the page,
candidate list, bulk submission, and progress reads to SuperAdmin only while
keeping every operation scoped to the application currently selected in
BackOffice.

### Phase 3: Bulk Queue Submission

Implement a bounded bulk use case that accepts deduplicated content IDs,
revalidates every candidate on the server, and reuses the existing idempotent
translation-request workflow. Return a safe result for each requested content
item without calling the provider from the HTTP request.

### Phase 4: Queue Dashboard UI

Build the page interface: type filter, search, paged candidate table,
checkboxes, selected-item count, and explicit queue confirmation. Clearly show
when queueing is unavailable because culture or worker prerequisites are not
configured.

### Phase 5: Live Job Progress

Add a SuperAdmin-only progress endpoint and browser polling every two to three
seconds. Update item states and aggregate counts in place; the progress bar
represents terminal content items, not an invented percentage for a provider
call.

### Phase 6: Validation And Hardening

Add focused application, repository, and Web tests for authorization, tenant
isolation, candidate filtering, batch limits, duplicate and concurrent requests,
source changes, worker outcomes, safe progress responses, and legacy Farsi byte
preservation.

### Phase 7: Controlled Rollout

Begin with a small batch, such as ten items, and verify resulting canonical
translations, UI progress, worker health, and provider cost. Increase the
configured maximum only after stable results and failure patterns are understood.
