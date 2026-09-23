-- FarsiContent data readiness audit
-- Version: 1.0 (see docs/farsi-content-readiness-policy.md)
--
-- READ-ONLY / non-destructive: every query here is a SELECT (optionally via OPENJSON/ISJSON for
-- JSON inspection) against CMS_Contents and its live section/element children. It creates,
-- alters, executes, and modifies nothing, and is safe to run against production.
--
-- Usage: run the whole script, or run one section at a time (each is self-contained). Table/
-- column names match Infrastructure/Data/Configurations/CMS/{Content,ContentSection,
-- SectionElement}Configuration.cs: CMS_Contents.FarsiContent (nvarchar(max), unconfigured =
-- convention-mapped), CMS_ContentSections (Id, ContentId), CMS_SectionElements (Id, SectionId).
-- Every table here derives from BaseEntity, so IsDeleted = 0 excludes soft-deleted rows exactly
-- as EF's global query filter would.

-- 1) FarsiContent state summary ---------------------------------------------------------------
-- Null / empty / valid-JSON / invalid-JSON counts, cross-tabbed with IsActive, plus payload size
-- (DATALENGTH, in bytes) per state. This is the primary readiness signal for Phase 0's activation
-- fallback: "Null"/"Empty" rows take the translate-then-activate path; "ValidJson" rows should
-- activate as-is without retranslating; "InvalidJson" rows must be flagged for human review (see
-- §3) rather than auto-repaired or silently overwritten.
;WITH FarsiClassified AS (
    SELECT
        c.Id,
        c.ApplicationId,
        c.IsActive,
        CASE
            WHEN c.FarsiContent IS NULL THEN 'Null'
            WHEN LTRIM(RTRIM(c.FarsiContent)) = '' THEN 'Empty'
            WHEN ISJSON(c.FarsiContent) = 1 THEN 'ValidJson'
            ELSE 'InvalidJson'
        END AS FarsiState,
        DATALENGTH(c.FarsiContent) AS PayloadBytes
    FROM CMS_Contents c
    WHERE c.IsDeleted = 0
)
SELECT
    FarsiState,
    COUNT(*) AS RowCount,
    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveCount,
    SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) AS InactiveCount,
    MIN(PayloadBytes) AS MinPayloadBytes,
    MAX(PayloadBytes) AS MaxPayloadBytes,
    AVG(PayloadBytes) AS AvgPayloadBytes
FROM FarsiClassified
GROUP BY FarsiState
ORDER BY FarsiState;

-- 2) Payload size distribution (ValidJson rows only) -------------------------------------------
-- Buckets stored Farsi snapshot size to catch outliers (e.g. an accidental double-encode, or a
-- pathologically large content graph) before Phase 0 relies on parsing every valid row.
;WITH SizedFarsi AS (
    SELECT DATALENGTH(c.FarsiContent) AS PayloadBytes
    FROM CMS_Contents c
    WHERE c.IsDeleted = 0 AND c.FarsiContent IS NOT NULL AND ISJSON(c.FarsiContent) = 1
)
SELECT
    CASE
        WHEN PayloadBytes < 1024 THEN '< 1 KB'
        WHEN PayloadBytes < 10 * 1024 THEN '1-10 KB'
        WHEN PayloadBytes < 100 * 1024 THEN '10-100 KB'
        WHEN PayloadBytes < 1024 * 1024 THEN '100 KB-1 MB'
        ELSE '>= 1 MB'
    END AS SizeBucket,
    COUNT(*) AS RowCount
FROM SizedFarsi
GROUP BY
    CASE
        WHEN PayloadBytes < 1024 THEN '< 1 KB'
        WHEN PayloadBytes < 10 * 1024 THEN '1-10 KB'
        WHEN PayloadBytes < 100 * 1024 THEN '10-100 KB'
        WHEN PayloadBytes < 1024 * 1024 THEN '100 KB-1 MB'
        ELSE '>= 1 MB'
    END
ORDER BY MIN(PayloadBytes);

-- 3) Malformed legacy blobs: flag for human review, do not auto-repair -------------------------
-- Every non-empty CMS_Contents.FarsiContent value that fails ISJSON. Phase 0 policy is to leave
-- these rows untouched and surface them for manual review - never to silently reset, reparse, or
-- overwrite production data. PayloadPreview is truncated to 200 chars purely to keep result rows
-- scannable; it is not a redaction and this script performs no write of any kind.
SELECT
    c.Id AS ContentId,
    c.ApplicationId,
    c.IsActive,
    DATALENGTH(c.FarsiContent) AS PayloadBytes,
    LEFT(c.FarsiContent, 200) AS PayloadPreview
FROM CMS_Contents c
WHERE c.IsDeleted = 0
  AND c.FarsiContent IS NOT NULL
  AND LTRIM(RTRIM(c.FarsiContent)) <> ''
  AND ISJSON(c.FarsiContent) = 0
ORDER BY c.Id;

-- 4) Farsi section IDs vs. current master graph (ValidJson rows only) ---------------------------
-- FarsiContentMapper.SerializeForStorage round-trips a whole Content graph (Sections[].Id,
-- Sections[].Elements[].Id) into CMS_Contents.FarsiContent as JSON - see
-- Web/Areas/BackOffice/Features/Content/FarsiContentMapper.cs. A stored Farsi snapshot is a
-- point-in-time copy: it can drift from the live CMS_ContentSections/CMS_SectionElements rows if
-- sections/elements were added, removed, or reordered on the English side afterward. This lists
-- every section ID present in only one side (Farsi snapshot vs. current master).
;WITH ValidFarsi AS (
    SELECT c.Id AS ContentId, c.FarsiContent
    FROM CMS_Contents c
    WHERE c.IsDeleted = 0 AND c.FarsiContent IS NOT NULL AND ISJSON(c.FarsiContent) = 1
),
FarsiSections AS (
    SELECT vf.ContentId, s.FarsiSectionId
    FROM ValidFarsi vf
    CROSS APPLY OPENJSON(vf.FarsiContent, '$.Sections')
        WITH (FarsiSectionId INT '$.Id') AS s
),
MasterSections AS (
    SELECT cs.ContentId, cs.Id AS MasterSectionId
    FROM CMS_ContentSections cs
    WHERE cs.IsDeleted = 0
)
SELECT
    COALESCE(fs.ContentId, ms.ContentId) AS ContentId,
    fs.FarsiSectionId,
    ms.MasterSectionId,
    CASE
        WHEN fs.FarsiSectionId IS NULL THEN 'MissingFromFarsiSnapshot' -- master added a section after translation
        WHEN ms.MasterSectionId IS NULL THEN 'StaleInFarsiSnapshot'    -- Farsi snapshot references a section no longer live
    END AS Discrepancy
FROM FarsiSections fs
FULL OUTER JOIN MasterSections ms
    ON fs.ContentId = ms.ContentId AND fs.FarsiSectionId = ms.MasterSectionId
WHERE fs.FarsiSectionId IS NULL OR ms.MasterSectionId IS NULL
ORDER BY ContentId;

-- 5) Farsi element IDs vs. current master graph (ValidJson rows only) ---------------------------
-- Same idea as §4, one level deeper: Sections[].Elements[].Id vs. live CMS_SectionElements.
;WITH ValidFarsi AS (
    SELECT c.Id AS ContentId, c.FarsiContent
    FROM CMS_Contents c
    WHERE c.IsDeleted = 0 AND c.FarsiContent IS NOT NULL AND ISJSON(c.FarsiContent) = 1
),
FarsiSectionsRaw AS (
    SELECT vf.ContentId, s.FarsiSectionId, s.ElementsJson
    FROM ValidFarsi vf
    CROSS APPLY OPENJSON(vf.FarsiContent, '$.Sections')
        WITH (FarsiSectionId INT '$.Id', ElementsJson NVARCHAR(MAX) '$.Elements' AS JSON) AS s
),
FarsiElements AS (
    SELECT fsr.ContentId, fsr.FarsiSectionId, e.FarsiElementId
    FROM FarsiSectionsRaw fsr
    CROSS APPLY OPENJSON(fsr.ElementsJson) WITH (FarsiElementId INT '$.Id') AS e
),
MasterElements AS (
    SELECT se.SectionId AS MasterSectionId, se.Id AS MasterElementId
    FROM CMS_SectionElements se
    WHERE se.IsDeleted = 0
)
SELECT
    fe.ContentId,
    COALESCE(fe.FarsiSectionId, me.MasterSectionId) AS SectionId,
    fe.FarsiElementId,
    me.MasterElementId,
    CASE
        WHEN fe.FarsiElementId IS NULL THEN 'MissingFromFarsiSnapshot'
        WHEN me.MasterElementId IS NULL THEN 'StaleInFarsiSnapshot'
    END AS Discrepancy
FROM FarsiElements fe
FULL OUTER JOIN MasterElements me
    ON fe.FarsiSectionId = me.MasterSectionId AND fe.FarsiElementId = me.MasterElementId
WHERE fe.FarsiElementId IS NULL OR me.MasterElementId IS NULL
ORDER BY fe.ContentId;
