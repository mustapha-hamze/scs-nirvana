-- Content Delivery SDK - Phase 0 SQL Server discovery script
-- Companion to docs/Content-Delivery-Discovery.md (section 8, DBA checklist).
--
-- READ-ONLY / non-destructive: SELECTs against catalog views (sys.*) and aggregate COUNTs over
-- CMS/GNR tables. It creates, alters, grants, and deletes nothing, and returns no content text,
-- titles, translation payloads, or user names beyond database principal names.
--
-- Run docs/inspect-schema.sql first: it already covers tables, columns, primary keys, foreign
-- keys (with delete rules), unique indexes, and SP_ContentsInCategory. This script covers only
-- what that one does not: every index on the delivery tables, database principals, role
-- memberships and permissions, Row-Level Security, objects that already read CMS tables, and
-- aggregate data-shape evidence behind the delivery rules.
--
-- Run as a principal with VIEW DEFINITION and VIEW DATABASE STATE (or db_owner on a restored
-- copy). Section 3b also needs VIEW ANY DEFINITION / securityadmin on the server; skip it if that
-- is not available. Each section is self-contained.

DECLARE @DeliveryTables TABLE (TableName sysname PRIMARY KEY);
INSERT INTO @DeliveryTables (TableName) VALUES
    ('CMS_Contents'), ('CMS_ContentMetadata'), ('CMS_ContentSections'), ('CMS_SectionElements'),
    ('CMS_ContentImages'), ('CMS_ContentAttachments'), ('CMS_ContentAttachmentItems'),
    ('CMS_Categories'), ('CMS_ContentInCategories'), ('CMS_ContentInTags'), ('CMS_ContentInCultures'),
    ('CMS_ContentTranslations'), ('GNR_Tags'), ('GNR_Cultures'), ('GNR_Applications');

-- 1) Delivery tables present, with row counts (catalog metadata, not a table scan) ------------
SELECT dt.TableName,
       CASE WHEN t.object_id IS NULL THEN 'MISSING' ELSE 'present' END AS State,
       SCHEMA_NAME(t.schema_id) AS SchemaName,
       (SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = t.object_id AND p.index_id IN (0, 1)) AS ApproxRows
FROM @DeliveryTables dt
LEFT JOIN sys.tables t ON t.name = dt.TableName
ORDER BY dt.TableName;

-- 2) Every index on the delivery tables (keys, included columns, filter) -----------------------
-- Compare with the delivery predicates: ApplicationId + IsDeleted + IsActive (+ TypeId) on
-- CMS_Contents; ContentId on every child table; SectionId on CMS_SectionElements;
-- CategoryId/TagId on the join tables; (ContentId, CultureId) on CMS_ContentTranslations.
SELECT t.name AS TableName,
       i.name AS IndexName,
       i.type_desc,
       i.is_primary_key,
       i.is_unique,
       i.has_filter,
       i.filter_definition,
       STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ')
           WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns,
       STRING_AGG(CASE WHEN ic.is_included_column = 1 THEN c.name END, ', ') AS IncludedColumns
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN @DeliveryTables dt ON dt.TableName = t.name
LEFT JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
LEFT JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.type > 0
GROUP BY t.name, i.name, i.type_desc, i.is_primary_key, i.is_unique, i.has_filter, i.filter_definition
ORDER BY t.name, i.name;

-- Foreign keys whose child column has no index leading with it (delivery joins walk these).
SELECT OBJECT_NAME(fk.parent_object_id) AS ChildTable,
       c.name AS ChildColumn,
       fk.name AS ForeignKeyName
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
JOIN @DeliveryTables dt ON dt.TableName = OBJECT_NAME(fk.parent_object_id)
WHERE NOT EXISTS (
    SELECT 1 FROM sys.index_columns ic
    WHERE ic.object_id = fkc.parent_object_id AND ic.column_id = fkc.parent_column_id
      AND ic.key_ordinal = 1)
ORDER BY ChildTable, ChildColumn;

-- 3a) Database principals and role memberships -------------------------------------------------
SELECT p.name AS PrincipalName,
       p.type_desc,
       p.authentication_type_desc,
       p.default_schema_name,
       SUSER_SNAME(p.sid) AS MappedLogin,
       r.name AS RoleName
FROM sys.database_principals p
LEFT JOIN sys.database_role_members rm ON rm.member_principal_id = p.principal_id
LEFT JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
WHERE p.type IN ('S', 'U', 'G', 'E', 'X', 'C', 'K', 'R')
  AND p.name NOT IN ('INFORMATION_SCHEMA', 'sys', 'guest', 'public')
ORDER BY p.name, r.name;

-- 3b) Server logins mapped into this database (needs server-level VIEW permission) -------------
SELECT sp.name AS LoginName, sp.type_desc, sp.is_disabled, sp.default_database_name,
       dp.name AS DatabaseUser
FROM sys.server_principals sp
JOIN sys.database_principals dp ON dp.sid = sp.sid
WHERE sp.type IN ('S', 'U', 'G', 'E', 'X')
ORDER BY sp.name;

-- 3c) Explicit permissions: database-level, schema-level, and on delivery objects --------------
SELECT pr.name AS Grantee,
       pr.type_desc AS GranteeType,
       pe.class_desc,
       CASE pe.class
           WHEN 0 THEN DB_NAME()
           WHEN 1 THEN OBJECT_SCHEMA_NAME(pe.major_id) + '.' + OBJECT_NAME(pe.major_id)
           WHEN 3 THEN SCHEMA_NAME(pe.major_id)
       END AS Securable,
       COL_NAME(pe.major_id, NULLIF(pe.minor_id, 0)) AS ColumnName,
       pe.permission_name,
       pe.state_desc
FROM sys.database_permissions pe
JOIN sys.database_principals pr ON pr.principal_id = pe.grantee_principal_id
WHERE pe.class IN (0, 1, 3)
  AND pr.name NOT IN ('public', 'guest')
ORDER BY pr.name, pe.class_desc, Securable, pe.permission_name;

-- 3d) Effective permissions of every SQL/Windows user on each delivery table -------------------
-- Includes role-inherited grants. EXECUTE AS USER needs IMPERSONATE; the loop REVERTs after
-- each user. HAS_PERMS_BY_NAME only reads metadata.
DECLARE @Effective TABLE (UserName sysname, TableName sysname, CanSelect bit, CanInsert bit,
                          CanUpdate bit, CanDelete bit, CanAlter bit);
DECLARE @User sysname;
DECLARE users CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.database_principals
    WHERE type IN ('S', 'U', 'E', 'X') AND name NOT IN ('dbo', 'guest', 'INFORMATION_SCHEMA', 'sys')
      AND sid IS NOT NULL;
OPEN users;
FETCH NEXT FROM users INTO @User;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        EXECUTE AS USER = @User;
        INSERT INTO @Effective
        SELECT @User, dt.TableName,
               HAS_PERMS_BY_NAME(dt.TableName, 'OBJECT', 'SELECT'),
               HAS_PERMS_BY_NAME(dt.TableName, 'OBJECT', 'INSERT'),
               HAS_PERMS_BY_NAME(dt.TableName, 'OBJECT', 'UPDATE'),
               HAS_PERMS_BY_NAME(dt.TableName, 'OBJECT', 'DELETE'),
               HAS_PERMS_BY_NAME(dt.TableName, 'OBJECT', 'ALTER')
        FROM @DeliveryTables dt
        WHERE OBJECT_ID(dt.TableName) IS NOT NULL;
        REVERT;
    END TRY
    BEGIN CATCH
        IF USER_NAME() = @User REVERT;
        INSERT INTO @Effective (UserName, TableName) VALUES (@User, '<could not impersonate>');
    END CATCH;
    FETCH NEXT FROM users INTO @User;
END;
CLOSE users;
DEALLOCATE users;
SELECT * FROM @Effective ORDER BY UserName, TableName;

-- 4) Row-Level Security: policies, predicates, and predicate function definitions --------------
SELECT sp.name AS PolicyName, sp.is_enabled, sp.is_schema_bound,
       OBJECT_NAME(pr.target_object_id) AS TargetTable,
       pr.predicate_type_desc, pr.operation_desc,
       pr.predicate_definition
FROM sys.security_policies sp
LEFT JOIN sys.security_predicates pr ON pr.object_id = sp.object_id
ORDER BY sp.name, TargetTable;

SELECT DISTINCT o.name AS PredicateFunction, OBJECT_DEFINITION(o.object_id) AS Definition
FROM sys.security_predicates pr
CROSS APPLY (SELECT d.referenced_id FROM sys.sql_expression_dependencies d
             WHERE d.referencing_id = pr.object_id) ref
JOIN sys.objects o ON o.object_id = ref.referenced_id AND o.type IN ('IF', 'FN', 'TF');

-- 5) Views, procedures, and functions that already read delivery tables -----------------------
-- Candidates for (or conflicts with) tenant-scoped delivery views/procedures.
SELECT DISTINCT OBJECT_SCHEMA_NAME(d.referencing_id) AS SchemaName,
       OBJECT_NAME(d.referencing_id) AS ObjectName,
       o.type_desc,
       d.referenced_entity_name AS ReadsTable
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON o.object_id = d.referencing_id
JOIN @DeliveryTables dt ON dt.TableName = d.referenced_entity_name
ORDER BY ObjectName, ReadsTable;

-- 6) Data-shape evidence for the delivery rules (aggregates only) ------------------------------

-- 6a) Content visibility states per application and type (inactive-but-live risk, rule V1).
SELECT ApplicationId, TypeId,
       SUM(CASE WHEN IsDeleted = 0 AND IsActive = 1 THEN 1 ELSE 0 END) AS ActiveLive,
       SUM(CASE WHEN IsDeleted = 0 AND IsActive = 0 THEN 1 ELSE 0 END) AS InactiveLive,
       SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS SoftDeleted
FROM CMS_Contents
GROUP BY ApplicationId, TypeId
ORDER BY ApplicationId, TypeId;

-- 6b) PublishDt quality (rules V4 and O2): default/sentinel, future, and later-than-update rows.
SELECT ApplicationId,
       COUNT(*) AS LiveContents,
       SUM(CASE WHEN PublishDt IS NULL OR PublishDt < '1901-01-01' THEN 1 ELSE 0 END) AS PublishDtUnset,
       SUM(CASE WHEN PublishDt > SYSUTCDATETIME() THEN 1 ELSE 0 END) AS PublishDtInFuture,
       SUM(CASE WHEN PublishDt > UpdatedDT THEN 1 ELSE 0 END) AS PublishDtAfterUpdated
FROM CMS_Contents
WHERE IsDeleted = 0
GROUP BY ApplicationId
ORDER BY ApplicationId;

-- 6c) Inactive descendants under active, live content (rule V2).
SELECT c.ApplicationId,
       SUM(CASE WHEN s.IsActive = 0 THEN 1 ELSE 0 END) AS InactiveSections,
       (SELECT COUNT(*) FROM CMS_SectionElements e
        JOIN CMS_ContentSections s2 ON s2.Id = e.SectionId AND s2.IsDeleted = 0
        JOIN CMS_Contents c2 ON c2.Id = s2.ContentId AND c2.IsDeleted = 0 AND c2.IsActive = 1
        WHERE c2.ApplicationId = c.ApplicationId AND e.IsDeleted = 0 AND e.IsActive = 0) AS InactiveElements,
       (SELECT COUNT(*) FROM CMS_ContentImages i
        JOIN CMS_Contents c3 ON c3.Id = i.ContentId AND c3.IsDeleted = 0 AND c3.IsActive = 1
        WHERE c3.ApplicationId = c.ApplicationId AND i.IsDeleted = 0 AND i.IsActive = 0) AS InactiveImages
FROM CMS_Contents c
LEFT JOIN CMS_ContentSections s ON s.ContentId = c.Id AND s.IsDeleted = 0
WHERE c.IsDeleted = 0 AND c.IsActive = 1
GROUP BY c.ApplicationId
ORDER BY c.ApplicationId;

-- 6d) Cross-tenant relations: join rows linking content to another application's term (T2).
SELECT 'category' AS Relation, c.ApplicationId AS ContentApplication, k.ApplicationId AS TermApplication, COUNT(*) AS RelationRows
FROM CMS_ContentInCategories r
JOIN CMS_Contents c ON c.Id = r.ContentId
JOIN CMS_Categories k ON k.Id = r.CategoryId
WHERE c.ApplicationId <> k.ApplicationId
GROUP BY c.ApplicationId, k.ApplicationId
UNION ALL
SELECT 'tag', c.ApplicationId, g.ApplicationId, COUNT(*)
FROM CMS_ContentInTags r
JOIN CMS_Contents c ON c.Id = r.ContentId
JOIN GNR_Tags g ON g.Id = r.TagId
WHERE c.ApplicationId <> g.ApplicationId
GROUP BY c.ApplicationId, g.ApplicationId;

-- 6e) Category hierarchy integrity (no FK on ParentId; decision D5).
SELECT k.ApplicationId,
       SUM(CASE WHEN k.ParentId = 0 THEN 1 ELSE 0 END) AS Roots,
       SUM(CASE WHEN k.ParentId <> 0 AND p.Id IS NULL THEN 1 ELSE 0 END) AS MissingParent,
       SUM(CASE WHEN p.Id IS NOT NULL AND p.ApplicationId <> k.ApplicationId THEN 1 ELSE 0 END) AS CrossTenantParent,
       SUM(CASE WHEN p.Id IS NOT NULL AND (p.IsDeleted = 1 OR p.IsActive = 0) THEN 1 ELSE 0 END) AS HiddenParent,
       SUM(CASE WHEN k.ParentId = k.Id THEN 1 ELSE 0 END) AS SelfParent
FROM CMS_Categories k
LEFT JOIN CMS_Categories p ON p.Id = k.ParentId
WHERE k.IsDeleted = 0 AND k.IsActive = 1
GROUP BY k.ApplicationId
ORDER BY k.ApplicationId;

-- 6f) Culture keys per application: duplicates make culture resolution ambiguous (rule C2).
SELECT ApplicationId, LOWER([Key]) AS CultureKey,
       SUM(CASE WHEN IsActive = 1 AND IsDeleted = 0 THEN 1 ELSE 0 END) AS ActiveRows,
       COUNT(*) AS AllRows
FROM GNR_Cultures
GROUP BY ApplicationId, LOWER([Key])
ORDER BY ApplicationId, CultureKey;

-- 6g) Canonical translation readiness per application and culture (rule C3).
-- TranslationStatus: 0 Draft, 1 Queued, 2 Translating, 3 Ready, 4 Stale, 5 Failed, 6 NeedsReview.
SELECT c.ApplicationId, t.CultureId, t.TranslationStatus, COUNT(*) AS TranslationRows,
       SUM(CASE WHEN ISJSON(t.LocalizedTextJson) = 1 THEN 0 ELSE 1 END) AS NotJson
FROM CMS_ContentTranslations t
JOIN CMS_Contents c ON c.Id = t.ContentId AND c.IsDeleted = 0
WHERE t.IsDeleted = 0
GROUP BY c.ApplicationId, t.CultureId, t.TranslationStatus
ORDER BY c.ApplicationId, t.CultureId, t.TranslationStatus;

-- Legacy snapshot coverage for live content (rule C4; docs/farsi-content-audit.sql has detail).
SELECT ApplicationId,
       SUM(CASE WHEN FarsiContent IS NULL OR LTRIM(RTRIM(FarsiContent)) = '' THEN 0 ELSE 1 END) AS WithLegacyFarsi,
       SUM(CASE WHEN FarsiContent IS NOT NULL AND ISJSON(FarsiContent) = 0 THEN 1 ELSE 0 END) AS LegacyNotJson
FROM CMS_Contents
WHERE IsDeleted = 0 AND IsActive = 1
GROUP BY ApplicationId
ORDER BY ApplicationId;

-- 6h) Ordering-key ties within an application (rule O1: why an Id tie-breaker is required).
SELECT ApplicationId, 'UpdatedDT' AS OrderKey, COUNT(*) AS TiedGroups
FROM (SELECT ApplicationId, UpdatedDT FROM CMS_Contents WHERE IsDeleted = 0 AND IsActive = 1
      GROUP BY ApplicationId, UpdatedDT HAVING COUNT(*) > 1) x
GROUP BY ApplicationId
UNION ALL
SELECT ApplicationId, 'PublishDt', COUNT(*)
FROM (SELECT ApplicationId, PublishDt FROM CMS_Contents WHERE IsDeleted = 0 AND IsActive = 1
      GROUP BY ApplicationId, PublishDt HAVING COUNT(*) > 1) x
GROUP BY ApplicationId
UNION ALL
SELECT c.ApplicationId, 'Section Priority', COUNT(*)
FROM (SELECT ContentId, Priority FROM CMS_ContentSections WHERE IsDeleted = 0
      GROUP BY ContentId, Priority HAVING COUNT(*) > 1) x
JOIN CMS_Contents c ON c.Id = x.ContentId AND c.IsDeleted = 0
GROUP BY c.ApplicationId
ORDER BY ApplicationId, OrderKey;
