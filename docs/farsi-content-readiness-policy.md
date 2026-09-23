# FarsiContent Phase 0 readiness policy

**Version:** 1.0
**Scope:** `CMS_Contents.FarsiContent` (see `Infrastructure/Data/Configurations/CMS/ContentConfiguration.cs`)
and the activation flow that reads/writes it
(`Web/Areas/BackOffice/Controllers/Content/ContentController.Forms.cs#ChangeContentActiveMode`,
`Application/UseCases/CMSServices/ContentServices.cs`).
**Companion script:** `docs/farsi-content-audit.sql` — run it against production (or the closest
pre-prod copy) and compare the results against §2 before relying on any claim below.

## 1. What Phase 0 does and does not change

Phase 0 does not change any public API contract. The legacy API continues to return English
content plus the raw `FarsiContent` field exactly as today; which language is shown remains a
client-side selection, not a server-side one. Nothing here adds, removes, or renames a field on
any response.

What Phase 0 fixes is server-side: the activation fallback below, which decides what
`ChangeContentActiveMode(mode: true)` does with a content's existing Farsi state.

## 2. Reviewed activation fallback

`ChangeContentActiveMode(contentId, mode: true, applicationId)` branches on the current
`CMS_Contents.FarsiContent` value for that row:

| Stored `FarsiContent` | Action | Translation provider called? | Stored Farsi payload touched? |
|---|---|---|---|
| `NULL` or empty | Translate via `IContentTranslator`, structurally validate, persist via `ActivateTranslatedContent`, activate only on success. | Yes | Yes (written) |
| Non-empty (regardless of JSON validity) | Activate through the existing tenant-scoped `IContentServices.ChangeContentActiveMode`. | No | No |
| Translation fails (empty-Farsi path only) | Content is **not** activated. | Yes (attempted) | No |

Rationale: a non-empty `FarsiContent` means either a manually saved translation or a prior
successful auto-translation. Phase 0 must never re-translate or overwrite either — re-translating
on every activation would silently discard manual edits and make activation nondeterministic
(a different machine translation each time). Activation is therefore keyed purely on "is there
already a Farsi snapshot", not on whether that snapshot is well-formed JSON — see §3 for why
malformed rows still activate.

Deactivation (`mode: false`), access checks (`[RequireAccess(AccessKeys.Content.ChangeActivity)]`),
tenant scoping (`ICurrentApplicationContext.RequireApplicationId()`), and the global MVC
antiforgery convention are all unchanged by Phase 0.

## 3. Malformed legacy blobs: reviewed, not repaired

`docs/farsi-content-audit.sql` §3 lists every row where `FarsiContent` is non-empty but fails
`ISJSON`. Phase 0 policy is to leave these rows exactly as they are:

- They still activate normally (§2's non-empty branch does not parse `FarsiContent` — it just
  flips `IsActive` through `ContentServices.ChangeContentActiveMode`, which never reads the field).
- They are **not** auto-reset to empty, auto-repaired, or overwritten by any Phase 0 code path.
- Human review, using §3's output (`ContentId`, `ApplicationId`, payload size, a 200-char preview),
  decides case by case whether to re-save the Farsi form (which re-serializes a clean payload) or
  leave the row alone.

This mirrors the existing `FarsiContentMapper.GetEditSource` behavior in the edit form: a JSON
parse failure there already falls back to an English-content clone rather than throwing or
mutating the stored row — Phase 0's activation path keeps the same "never silently rewrite
production data" posture.

## 4. Section/element drift

A stored Farsi snapshot is a point-in-time serialization of the whole `Content` graph
(`Sections[].Id`, `Sections[].Elements[].Id`) — see `FarsiContentMapper.SerializeForStorage`. If
sections or elements are added/removed on the English side after a Farsi snapshot was saved, that
snapshot silently drifts from the live `CMS_ContentSections`/`CMS_SectionElements` rows. Phase 0
does not resolve this drift automatically. `docs/farsi-content-audit.sql` §4–§5 surface it
(`StaleInFarsiSnapshot` / `MissingFromFarsiSnapshot`) for human review; resolving it is an editorial
action (re-open and re-save the Farsi form), not something the activation endpoint should attempt.

## 5. How to use this together with the script

1. Run `docs/farsi-content-audit.sql` §1 for the overall null/empty/valid/invalid/active
   breakdown and payload sizes.
2. Run §3 to get the malformed-blob review list; route it to whoever owns Farsi content editorial
   review.
3. Run §4–§5 to get the section/element drift list for the same review.
4. Nothing in this script or policy requires or performs a write. Any resulting fix (re-saving a
   Farsi form, or an explicit data migration) is a separate, deliberate change — never a
   side effect of running the audit or of activating content.
