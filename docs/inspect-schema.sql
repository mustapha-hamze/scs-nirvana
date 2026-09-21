-- Core schema-readiness inspection script
-- Version: 1.0 (see docs/schema-readiness.md)
--
-- READ-ONLY / non-destructive: every query here is a SELECT against catalog views
-- (INFORMATION_SCHEMA / sys.*). It creates, alters, and deletes nothing, and is safe to run
-- against production. Run it against the real SQL Server database and compare the results to
-- docs/schema-readiness.md before authoring or reviewing any additive EF migration.
--
-- Usage: run the whole script, or run one section at a time (each is self-contained). Every
-- query is scoped to the table-name prefixes this app actually uses (CMS_, GNR_, AME_, SCM_) so
-- it doesn't dump unrelated tables in a shared database.

DECLARE @TablePrefixes TABLE (Prefix nvarchar(10));
INSERT INTO @TablePrefixes (Prefix) VALUES ('CMS_'), ('GNR_'), ('AME_'), ('SCM_');

-- 1) Tables ---------------------------------------------------------------------------------
-- Every table this app's EF model expects to exist. Compare the row count/names against
-- ApplicationDbContext's DbSet<T> list and docs/schema-readiness.md's table inventory.
SELECT t.TABLE_SCHEMA, t.TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES t
WHERE t.TABLE_TYPE = 'BASE TABLE'
  AND EXISTS (SELECT 1 FROM @TablePrefixes p WHERE t.TABLE_NAME LIKE p.Prefix + '%')
ORDER BY t.TABLE_NAME;

-- 2) Columns: types, nullability, lengths ----------------------------------------------------
-- Compare against each entity's [StringLength]/[Required] attributes and its
-- IEntityTypeConfiguration<T> (Infrastructure/Data/Configurations/**), including the shared
-- BaseEntity audit columns (Id, Status, IsDeleted, IsActive, UpdatedDT, CreatedDT) that every
-- ConfigureAudit<T> call declares as required.
SELECT
    c.TABLE_NAME,
    c.COLUMN_NAME,
    c.ORDINAL_POSITION,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE EXISTS (SELECT 1 FROM @TablePrefixes p WHERE c.TABLE_NAME LIKE p.Prefix + '%')
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;

-- 3) Primary keys -----------------------------------------------------------------------------
SELECT
    tc.TABLE_NAME,
    kcu.COLUMN_NAME,
    tc.CONSTRAINT_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
    ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
  AND EXISTS (SELECT 1 FROM @TablePrefixes p WHERE tc.TABLE_NAME LIKE p.Prefix + '%')
ORDER BY tc.TABLE_NAME;

-- 4) Foreign keys and their delete rules --------------------------------------------------------
-- Compare against Infrastructure/Data/Configurations/**: only the three ContentIn* join-table
-- configs (Category/Tag/Culture) explicitly call .OnDelete(DeleteBehavior.Cascade) today. Every
-- other relationship (e.g. Content -> ContentSection -> SectionElement) relies on EF Core's
-- convention default (Cascade for a required/non-nullable FK, ClientSetNull for an optional
-- one) rather than an explicit .OnDelete(...) call - this query is what actually confirms
-- whether that assumption matches the live database, since production schema is externally
-- managed and was never generated from this EF model.
SELECT
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    cpa.name AS ChildColumn,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
    cref.name AS ParentColumn,
    fk.delete_referential_action_desc AS OnDeleteAction,
    fk.update_referential_action_desc AS OnUpdateAction
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cpa ON cpa.object_id = fk.parent_object_id AND cpa.column_id = fkc.parent_column_id
JOIN sys.columns cref ON cref.object_id = fk.referenced_object_id AND cref.column_id = fkc.referenced_column_id
WHERE EXISTS (SELECT 1 FROM @TablePrefixes p WHERE OBJECT_NAME(fk.parent_object_id) LIKE p.Prefix + '%')
ORDER BY ChildTable, ForeignKeyName;

-- 5) Unique indexes/constraints -----------------------------------------------------------------
-- Confirms whether the two membership/access uniqueness constraints Core P0 added to the EF
-- model actually exist in the live database yet:
--   GNR_UserInApplications: unique on (UserId, ApplicationId)
--   GNR_UserAccesses:       unique on (UserId, ApplicationId)
-- plus the pre-existing CMS_ContentIn{Categories,Tags,Cultures} uniqueness on (ContentId, <RelatedId>).
-- If a row is missing here for GNR_UserInApplications/GNR_UserAccesses, the constraint is
-- currently enforced only by the EF model (and by SQLite in tests) - NOT by production - and the
-- additive migration described in docs/schema-readiness.md has not been applied yet.
SELECT
    t.name AS TableName,
    i.name AS IndexName,
    i.is_unique,
    i.is_unique_constraint,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.is_unique = 1
  AND EXISTS (SELECT 1 FROM @TablePrefixes p WHERE t.name LIKE p.Prefix + '%')
GROUP BY t.name, i.name, i.is_unique, i.is_unique_constraint
ORDER BY t.name, i.name;

-- 6) SP_ContentsInCategory: existence, definition, and parameters --------------------------------
-- IContentsInCategoryQueryAdapter.GetContentsInCategory calls this by name via Dapper
-- (commandType: StoredProcedure) with @P_CategoryId and @P_ApplicationId. This only confirms it
-- exists and its declared parameter shape; it does NOT show whether the procedure body itself
-- filters IsDeleted/IsActive on CMS_Contents (Dapper/raw-SQL reads bypass EF's global
-- soft-delete query filter entirely - see the comment on ContentsInCategoryQueryAdapter).
SELECT
    o.name AS ProcedureName,
    o.create_date,
    o.modify_date
FROM sys.objects o
WHERE o.type = 'P' AND o.name = 'SP_ContentsInCategory';

SELECT
    p.name AS ParameterName,
    TYPE_NAME(p.user_type_id) AS DataType,
    p.max_length,
    p.is_output
FROM sys.parameters p
WHERE p.object_id = OBJECT_ID('SP_ContentsInCategory')
ORDER BY p.parameter_id;

-- Definition text, if the procedure isn't encrypted (WITH ENCRYPTION) - read-only, does not
-- execute the procedure.
SELECT OBJECT_DEFINITION(OBJECT_ID('SP_ContentsInCategory')) AS ProcedureDefinition;
