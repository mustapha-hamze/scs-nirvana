-- Content Delivery database hardening (Phase 6) - 01 read-only preflight
-- Companion to README.md in this folder.
--
-- READ-ONLY: SELECTs against catalog views and aggregate COUNTs. Creates, alters, grants and
-- deletes nothing, and returns no content text, titles, keys or translation payloads.
-- Run on the restored production backup first, then (unchanged) on production before deploying.
--
-- Usage (SQLCMD mode; every variable is required, none has a default):
--   sqlcmd -S <server> -E -b -i 01-preflight.sql \
--     -v CmsDatabase="<db>" WebsiteLoginName="<login>" WebsiteUserName="<user>" WebsiteApplicationId=<id>
--
-- Run as a principal with VIEW DEFINITION, VIEW DATABASE STATE and VIEW SERVER STATE
-- (sysadmin on the restored copy). Every result set has a Check column; compare it with the
-- expected results table in README.md section 4.

:on error exit
SET NOCOUNT ON;
USE [$(CmsDatabase)];

DECLARE @WebsiteLogin sysname = N'$(WebsiteLoginName)';
DECLARE @WebsiteUser sysname = N'$(WebsiteUserName)';
DECLARE @ApplicationId int = $(WebsiteApplicationId);

-- Tables the Content Delivery adapter reads (ContentDeliveryDbContext) and the columns it maps.
DECLARE @Delivery TABLE (TableName sysname NOT NULL, ColumnName sysname NOT NULL);
INSERT INTO @Delivery (TableName, ColumnName) VALUES
    (N'CMS_Contents', N'Id'), (N'CMS_Contents', N'ApplicationId'), (N'CMS_Contents', N'TypeId'),
    (N'CMS_Contents', N'Title'), (N'CMS_Contents', N'HeadLine'), (N'CMS_Contents', N'Abstract'),
    (N'CMS_Contents', N'Description'), (N'CMS_Contents', N'PublishDt'), (N'CMS_Contents', N'FarsiContent'),
    (N'CMS_Contents', N'IsActive'), (N'CMS_Contents', N'IsDeleted'), (N'CMS_Contents', N'UpdatedDT'),
    (N'CMS_ContentMetadata', N'Id'), (N'CMS_ContentMetadata', N'ContentId'), (N'CMS_ContentMetadata', N'Title'),
    (N'CMS_ContentMetadata', N'Author'), (N'CMS_ContentMetadata', N'Keywords'),
    (N'CMS_ContentMetadata', N'Description'), (N'CMS_ContentMetadata', N'IsDeleted'),
    (N'CMS_ContentSections', N'Id'), (N'CMS_ContentSections', N'ContentId'), (N'CMS_ContentSections', N'Priority'),
    (N'CMS_ContentSections', N'IsActive'), (N'CMS_ContentSections', N'IsDeleted'),
    (N'CMS_SectionElements', N'Id'), (N'CMS_SectionElements', N'SectionId'), (N'CMS_SectionElements', N'ElementType'),
    (N'CMS_SectionElements', N'ElementTitle'), (N'CMS_SectionElements', N'TinyText'),
    (N'CMS_SectionElements', N'EditorText'), (N'CMS_SectionElements', N'FileNameText'),
    (N'CMS_SectionElements', N'GalleryImages'), (N'CMS_SectionElements', N'Size'),
    (N'CMS_SectionElements', N'IsActive'), (N'CMS_SectionElements', N'IsDeleted'),
    (N'CMS_ContentImages', N'Id'), (N'CMS_ContentImages', N'ContentId'), (N'CMS_ContentImages', N'ImageFileName'),
    (N'CMS_ContentImages', N'Size'), (N'CMS_ContentImages', N'IsActive'), (N'CMS_ContentImages', N'IsDeleted'),
    (N'CMS_Categories', N'Id'), (N'CMS_Categories', N'ApplicationId'), (N'CMS_Categories', N'ParentId'),
    (N'CMS_Categories', N'Title'), (N'CMS_Categories', N'Description'), (N'CMS_Categories', N'IsActive'),
    (N'CMS_Categories', N'IsDeleted'),
    (N'GNR_Tags', N'Id'), (N'GNR_Tags', N'ApplicationId'), (N'GNR_Tags', N'Title'), (N'GNR_Tags', N'IsActive'),
    (N'GNR_Tags', N'IsDeleted'),
    (N'CMS_ContentInCategories', N'Id'), (N'CMS_ContentInCategories', N'ContentId'),
    (N'CMS_ContentInCategories', N'CategoryId'),
    (N'CMS_ContentInTags', N'Id'), (N'CMS_ContentInTags', N'ContentId'), (N'CMS_ContentInTags', N'TagId'),
    (N'GNR_Cultures', N'Id'), (N'GNR_Cultures', N'ApplicationId'), (N'GNR_Cultures', N'Key'),
    (N'GNR_Cultures', N'IsActive'), (N'GNR_Cultures', N'IsDeleted'),
    (N'CMS_ContentTranslations', N'Id'), (N'CMS_ContentTranslations', N'ContentId'),
    (N'CMS_ContentTranslations', N'CultureId'), (N'CMS_ContentTranslations', N'TranslationStatus'),
    (N'CMS_ContentTranslations', N'SourceFingerprint'), (N'CMS_ContentTranslations', N'LocalizedTextJson'),
    (N'CMS_ContentTranslations', N'IsDeleted'), (N'CMS_ContentTranslations', N'UpdatedDT');

-- 1) Server and database: RLS needs SQL Server 2016 (major version 13) or later ----------------
SELECT N'1 platform' AS [Check],
       CAST(SERVERPROPERTY('ProductMajorVersion') AS int) AS ProductMajorVersion,
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(64)) AS ProductVersion,
       CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
       d.name AS DatabaseName, d.compatibility_level, d.is_read_only, d.state_desc,
       SUSER_SNAME(d.owner_sid) AS DatabaseOwner,
       CASE WHEN CAST(SERVERPROPERTY('ProductMajorVersion') AS int) >= 13 THEN N'OK' ELSE N'BLOCKER: RLS unsupported' END AS Verdict
FROM sys.databases d
WHERE d.database_id = DB_ID();

-- 2) Delivery tables and mapped columns: schema dbo, schema-owned by dbo, int predicate columns --
SELECT N'2 tables' AS [Check], x.TableName,
       SCHEMA_NAME(t.schema_id) AS SchemaName,
       COALESCE(USER_NAME(t.principal_id), N'<schema owner> ' + USER_NAME(s.principal_id)) AS TableOwner,
       CASE WHEN t.object_id IS NULL THEN N'BLOCKER: missing'
            WHEN SCHEMA_NAME(t.schema_id) <> N'dbo' THEN N'BLOCKER: not in dbo'
            WHEN t.principal_id IS NOT NULL OR s.principal_id <> USER_ID(N'dbo') THEN N'BLOCKER: not owned by dbo'
            WHEN t.is_memory_optimized = 1 OR t.filestream_data_space_id IS NOT NULL THEN N'REVIEW: memory-optimized/FILESTREAM'
            ELSE N'OK' END AS Verdict
FROM (SELECT DISTINCT TableName FROM @Delivery) x
LEFT JOIN sys.tables t ON t.name = x.TableName AND t.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY x.TableName;

SELECT N'2 columns' AS [Check], d.TableName, d.ColumnName, TYPE_NAME(c.user_type_id) AS DataType,
       CASE WHEN c.column_id IS NULL THEN N'BLOCKER: mapped column missing'
            WHEN d.ColumnName IN (N'Id', N'ApplicationId', N'ContentId', N'SectionId', N'CategoryId', N'TagId')
                 AND TYPE_NAME(c.user_type_id) <> N'int' THEN N'BLOCKER: predicate column is not int'
            ELSE N'OK' END AS Verdict
FROM @Delivery d
LEFT JOIN sys.columns c ON c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(d.TableName)) AND c.name = d.ColumnName
WHERE c.column_id IS NULL OR d.ColumnName IN (N'Id', N'ApplicationId', N'ContentId', N'SectionId', N'CategoryId', N'TagId')
ORDER BY d.TableName, d.ColumnName;

-- 3) Existing RLS and objects that would conflict with a schema-bound policy -------------------
SELECT N'3 existing policies' AS [Check], SCHEMA_NAME(sp.schema_id) + N'.' + sp.name AS PolicyName,
       sp.is_enabled, sp.is_schema_bound, OBJECT_NAME(pr.target_object_id) AS TargetTable,
       pr.predicate_type_desc, pr.operation_desc,
       CASE WHEN SCHEMA_NAME(sp.schema_id) = N'ContentDeliverySecurity' THEN N'INFO: this hardening already deployed'
            WHEN OBJECT_NAME(pr.target_object_id) IN (SELECT TableName FROM @Delivery) THEN N'BLOCKER: foreign policy on delivery table'
            ELSE N'INFO' END AS Verdict
FROM sys.security_policies sp
LEFT JOIN sys.security_predicates pr ON pr.object_id = sp.object_id;

-- Indexed views over delivery tables block adding a security policy.
SELECT N'3 indexed views' AS [Check], OBJECT_SCHEMA_NAME(v.object_id) + N'.' + v.name AS ViewName,
       d.referenced_entity_name AS DeliveryTable, N'BLOCKER: indexed view' AS Verdict
FROM sys.views v
JOIN sys.indexes i ON i.object_id = v.object_id AND i.index_id = 1
JOIN sys.sql_expression_dependencies d ON d.referencing_id = v.object_id
WHERE d.referenced_entity_name IN (SELECT TableName FROM @Delivery);

-- 4) Hardening objects that already exist (expected: none on first deploy) ---------------------
SELECT N'4 hardening objects' AS [Check], x.ObjectKind, x.ObjectName, x.ExistsNow,
       CASE WHEN x.ExistsNow = 0 THEN N'OK: absent'
            WHEN x.Tagged = 1 THEN N'INFO: created by this hardening'
            ELSE N'BLOCKER: exists but not created by this hardening' END AS Verdict
FROM (
    SELECT N'schema' AS ObjectKind, N'ContentDeliverySecurity' AS ObjectName,
           CAST(CASE WHEN SCHEMA_ID(N'ContentDeliverySecurity') IS NULL THEN 0 ELSE 1 END AS bit) AS ExistsNow,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.extended_properties ep WHERE ep.class = 3
                     AND ep.major_id = SCHEMA_ID(N'ContentDeliverySecurity') AND ep.name = N'ContentDeliveryHardening')
                THEN 1 ELSE 0 END AS bit) AS Tagged
    UNION ALL
    -- The role is tagged indirectly: it may exist only after the tagged schema was created.
    SELECT N'role', N'ContentDeliveryWebsiteReader',
           CAST(CASE WHEN DATABASE_PRINCIPAL_ID(N'ContentDeliveryWebsiteReader') IS NULL THEN 0 ELSE 1 END AS bit),
           CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.extended_properties ep WHERE ep.class = 3
                     AND ep.major_id = SCHEMA_ID(N'ContentDeliverySecurity') AND ep.name = N'ContentDeliveryHardening')
                THEN 1 ELSE 0 END AS bit)
    UNION ALL
    SELECT N'user', @WebsiteUser,
           CAST(CASE WHEN DATABASE_PRINCIPAL_ID(@WebsiteUser) IS NULL THEN 0 ELSE 1 END AS bit),
           CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.extended_properties ep WHERE ep.class = 4
                     AND ep.major_id = DATABASE_PRINCIPAL_ID(@WebsiteUser) AND ep.name = N'ContentDeliveryHardening')
                THEN 1 ELSE 0 END AS bit)
) x;

-- 5) Website login: must exist before deploy, be an individual SQL/Windows/Entra login, and hold
--    no server role (02-create-website-login.sql creates a SQL-auth one if needed).
SELECT N'5 website login' AS [Check], sp.name AS LoginName, sp.type_desc, sp.is_disabled,
       sp.default_database_name, sl.is_policy_checked, sl.is_expiration_checked,
       (SELECT COUNT(*) FROM sys.server_role_members rm WHERE rm.member_principal_id = sp.principal_id) AS ServerRoleCount,
       (SELECT COUNT(*) FROM sys.server_permissions pe WHERE pe.grantee_principal_id = sp.principal_id
            AND pe.permission_name NOT IN (N'CONNECT SQL') ) AS ExtraServerPermissionCount,
       CASE WHEN sp.principal_id IS NULL THEN N'INFO: create it with 02-create-website-login.sql'
            WHEN sp.type NOT IN ('S', 'U', 'E') THEN N'BLOCKER: group logins cannot be mapped 1:1'
            WHEN IS_SRVROLEMEMBER(N'sysadmin', sp.name) = 1 THEN N'BLOCKER: sysadmin'
            WHEN EXISTS (SELECT 1 FROM sys.server_role_members rm WHERE rm.member_principal_id = sp.principal_id)
                THEN N'BLOCKER: holds a server role'
            ELSE N'OK' END AS Verdict
FROM (SELECT @WebsiteLogin AS name) want
LEFT JOIN sys.server_principals sp ON sp.name = want.name
LEFT JOIN sys.sql_logins sl ON sl.principal_id = sp.principal_id;

-- Database user already mapped to the website login (other than the intended one) ------------
SELECT N'5 login mapping' AS [Check], dp.name AS DatabaseUser, dp.type_desc,
       CASE WHEN dp.name = @WebsiteUser THEN N'INFO: intended user' ELSE N'BLOCKER: login already mapped to another user' END AS Verdict
FROM sys.database_principals dp
WHERE dp.sid = SUSER_SID(@WebsiteLogin);

-- 6) Principals, role memberships and explicit permissions in this database -------------------
SELECT N'6 principals' AS [Check], p.name AS PrincipalName, p.type_desc, p.authentication_type_desc,
       SUSER_SNAME(p.sid) AS MappedLogin, r.name AS RoleName
FROM sys.database_principals p
LEFT JOIN sys.database_role_members rm ON rm.member_principal_id = p.principal_id
LEFT JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
WHERE p.type IN ('S', 'U', 'G', 'E', 'X', 'C', 'K', 'R')
  AND p.name NOT IN (N'INFORMATION_SCHEMA', N'sys', N'guest', N'public')
ORDER BY p.name, r.name;

SELECT N'6 permissions' AS [Check], pr.name AS Grantee, pe.class_desc,
       CASE pe.class WHEN 0 THEN DB_NAME()
                     WHEN 1 THEN OBJECT_SCHEMA_NAME(pe.major_id) + N'.' + OBJECT_NAME(pe.major_id)
                     WHEN 3 THEN SCHEMA_NAME(pe.major_id) END AS Securable,
       COL_NAME(pe.major_id, NULLIF(pe.minor_id, 0)) AS ColumnName, pe.permission_name, pe.state_desc,
       CASE WHEN pr.name IN (N'public', N'guest') AND pe.permission_name <> N'CONNECT' AND pe.class IN (0, 1, 3)
                 AND ISNULL(OBJECTPROPERTY(pe.major_id, 'IsMSShipped'), 0) = 0
            THEN N'REVIEW: inherited by every website identity' ELSE N'INFO' END AS Verdict
FROM sys.database_permissions pe
JOIN sys.database_principals pr ON pr.principal_id = pe.grantee_principal_id
WHERE pe.class IN (0, 1, 3)
  AND NOT (pr.name = N'public' AND pe.class = 1 AND ISNULL(OBJECTPROPERTY(pe.major_id, 'IsMSShipped'), 0) = 1)
ORDER BY pr.name, pe.class_desc, Securable, pe.permission_name;

-- 7) Tenants: target application and per-table tenant spread (counts only) ---------------------
SELECT N'7 target application' AS [Check], @ApplicationId AS ApplicationId,
       (SELECT COUNT(*) FROM dbo.GNR_Applications WHERE Id = @ApplicationId) AS ApplicationRows,
       (SELECT COUNT(*) FROM dbo.CMS_Contents WHERE ApplicationId = @ApplicationId) AS ContentRows,
       (SELECT COUNT(*) FROM dbo.CMS_Contents WHERE ApplicationId = @ApplicationId AND IsActive = 1 AND IsDeleted = 0) AS DeliverableContentRows,
       CASE WHEN @ApplicationId <= 0 THEN N'BLOCKER: application id must be positive'
            WHEN NOT EXISTS (SELECT 1 FROM dbo.GNR_Applications WHERE Id = @ApplicationId) THEN N'BLOCKER: application missing'
            ELSE N'OK' END AS Verdict;

SELECT N'7 tenant spread' AS [Check], x.TableName, x.DistinctApplications, x.RowsOutsideTarget,
       CASE WHEN x.DistinctApplications < 2 THEN N'NOTE: single tenant - isolation needs 04 with seeded rows'
            ELSE N'OK' END AS Verdict
FROM (
    SELECT N'CMS_Contents' AS TableName, COUNT(DISTINCT ApplicationId) AS DistinctApplications,
           SUM(CASE WHEN ApplicationId <> @ApplicationId THEN 1 ELSE 0 END) AS RowsOutsideTarget FROM dbo.CMS_Contents
    UNION ALL SELECT N'CMS_Categories', COUNT(DISTINCT ApplicationId),
           SUM(CASE WHEN ApplicationId <> @ApplicationId THEN 1 ELSE 0 END) FROM dbo.CMS_Categories
    UNION ALL SELECT N'GNR_Tags', COUNT(DISTINCT ApplicationId),
           SUM(CASE WHEN ApplicationId <> @ApplicationId THEN 1 ELSE 0 END) FROM dbo.GNR_Tags
) x;

SELECT N'7 cultures' AS [Check],
       SUM(CASE WHEN ApplicationId = 0 AND IsActive = 1 AND IsDeleted = 0 THEN 1 ELSE 0 END) AS ActiveGlobalCultures,
       SUM(CASE WHEN ApplicationId <> 0 THEN 1 ELSE 0 END) AS TenantOwnedCultures,
       CASE WHEN SUM(CASE WHEN ApplicationId = 0 AND IsActive = 1 AND IsDeleted = 0 THEN 1 ELSE 0 END) = 0
            THEN N'BLOCKER: no active global culture' ELSE N'OK' END AS Verdict
FROM dbo.GNR_Cultures;

-- 8) Other readers of the delivery tables (views/procs that would inherit the filter) ----------
SELECT N'8 dependent modules' AS [Check], OBJECT_SCHEMA_NAME(d.referencing_id) + N'.' + OBJECT_NAME(d.referencing_id) AS Module,
       o.type_desc, d.referenced_entity_name AS DeliveryTable, N'INFO' AS Verdict
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON o.object_id = d.referencing_id
WHERE d.referenced_entity_name IN (SELECT TableName FROM @Delivery)
  AND OBJECT_SCHEMA_NAME(d.referencing_id) <> N'ContentDeliverySecurity'
ORDER BY Module;
