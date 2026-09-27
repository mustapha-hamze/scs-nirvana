-- Content translation jobs - 01 add CMS_ContentTranslationJobs.TranslationPrecondition
-- For DBA review. Not run by the application; there are no EF migrations for this table.
--
-- Why: a job queued by the legacy-Farsi bulk queue records that no canonical translation row
-- existed when it was queued (TranslationPrecondition = 1). The worker then only ever INSERTs the
-- (ContentId, CultureId) row, so the existing unique index on CMS_ContentTranslations makes its
-- check-and-write atomic: a row created by anyone after the job was queued - including a
-- soft-deleted one - fails the worker's save, and the job ends Failed / translation_conflict
-- instead of overwriting it.
--
-- Values: 0 = None (normal request; may update an existing non-deleted row - the stale/failed
-- retry workflow), 1 = NoTranslation (bulk queue; insert only).
--
-- Additive and idempotent (each step checks before it changes anything; re-run after a partial run). Existing rows get 0 through the default, i.e. exactly their current
-- behaviour; no row is updated. Adding a NOT NULL column with a constant default is a
-- metadata-only change on SQL Server 2012+ Enterprise (and Azure SQL); on other editions it
-- rewrites the table - CMS_ContentTranslationJobs is small, but schedule accordingly.
--
-- Deployment order: run this BEFORE deploying the application build that maps the column (the
-- app reads and writes it on every job query/insert and fails without it). Rollback of the app
-- does not require dropping the column. Verify with the SELECT at the end.
--
-- Usage: sqlcmd -S <server> -E -b -i 01-add-job-translation-precondition.sql -v CmsDatabase="<db>"

:on error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
USE [$(CmsDatabase)];

IF OBJECT_ID(N'dbo.CMS_ContentTranslationJobs', N'U') IS NULL
    THROW 50001, 'dbo.CMS_ContentTranslationJobs does not exist; deploy the translation-job table first.', 1;

-- The atomic guard depends on this index: unique, unfiltered, exactly (ContentId, CultureId).
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.CMS_ContentTranslations') AND i.is_unique = 1 AND i.has_filter = 0
      AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0) = 2
      AND EXISTS (SELECT 1 FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                  AND ic.column_id = COLUMNPROPERTY(i.object_id, N'ContentId', 'ColumnId'))
      AND EXISTS (SELECT 1 FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                  AND ic.column_id = COLUMNPROPERTY(i.object_id, N'CultureId', 'ColumnId')))
    THROW 50002, 'dbo.CMS_ContentTranslations has no unfiltered unique index on (ContentId, CultureId); the bulk-queue guard requires it.', 1;

GO

-- Separate batches: a batch that names the new column can't compile before the column exists.
IF COL_LENGTH(N'dbo.CMS_ContentTranslationJobs', N'TranslationPrecondition') IS NULL
    ALTER TABLE dbo.CMS_ContentTranslationJobs
        ADD TranslationPrecondition tinyint NOT NULL
            CONSTRAINT DF_CMS_ContentTranslationJobs_TranslationPrecondition DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_CMS_ContentTranslationJobs_TranslationPrecondition'
                 AND parent_object_id = OBJECT_ID(N'dbo.CMS_ContentTranslationJobs'))
    ALTER TABLE dbo.CMS_ContentTranslationJobs WITH CHECK
        ADD CONSTRAINT CK_CMS_ContentTranslationJobs_TranslationPrecondition CHECK (TranslationPrecondition IN (0, 1));
GO

-- Verify: expect one row, tinyint, not nullable, default ((0)), and every existing job counted under 0.
SELECT c.name, t.name AS type_name, c.is_nullable, dc.definition AS default_definition
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
WHERE c.object_id = OBJECT_ID(N'dbo.CMS_ContentTranslationJobs') AND c.name = N'TranslationPrecondition';

SELECT TranslationPrecondition, COUNT(*) AS Jobs FROM dbo.CMS_ContentTranslationJobs GROUP BY TranslationPrecondition;
