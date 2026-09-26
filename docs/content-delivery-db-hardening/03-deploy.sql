-- Content Delivery database hardening (Phase 6) - 03 additive deployment
-- Companion to README.md in this folder. Review it with 01-preflight.sql results before running.
--
-- Additive only: creates the ContentDeliverySecurity schema (mapping table, predicate functions,
-- security policy), the ContentDeliveryWebsiteReader role, and one website database user mapped
-- to one application. It changes no CMS table, column, index, row or existing principal.
-- Idempotent: re-running with the same inputs changes nothing; running it again with another
-- website's inputs adds that website. Everything runs in one transaction (XACT_ABORT ON).
--
-- Usage:
--   sqlcmd -S <server> -E -b -i 03-deploy.sql \
--     -v CmsDatabase="<db>" WebsiteLoginName="<login>" WebsiteUserName="<user>" WebsiteApplicationId=<id>
--
-- Security model (README section 2):
--   * A caller is RESTRICTED when its database user (USER_NAME()) has a mapping row, or when it is
--     a member of ContentDeliveryWebsiteReader. A restricted caller sees only rows of its mapped
--     application and their descendants, plus global cultures (ApplicationId = 0). A role member
--     with no mapping row sees nothing.
--   * Every other principal (BackOffice, DBA, dbo) is unrestricted, so existing behaviour is
--     unchanged. Those identities must never be added to the role or the mapping.
--   * Scope comes only from the server/database identity. No predicate reads SESSION_CONTEXT,
--     CONTEXT_INFO, APP_NAME(), HOST_NAME() or any caller-supplied value.
--   * The policy is schema-bound, so predicate lookups need no caller permission; the website
--     role is explicitly denied every permission on the security schema.

:on error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
USE [$(CmsDatabase)];

DECLARE @Login sysname = N'$(WebsiteLoginName)';
DECLARE @User sysname = N'$(WebsiteUserName)';
DECLARE @ApplicationId int = $(WebsiteApplicationId);
DECLARE @Sql nvarchar(max);

BEGIN TRANSACTION;

------------------------------------------------------------------------------------------------
-- Guards: refuse anything the preflight marks as a BLOCKER.
------------------------------------------------------------------------------------------------
IF CAST(SERVERPROPERTY('ProductMajorVersion') AS int) < 13
    THROW 50301, N'Row-Level Security requires SQL Server 2016 or later.', 1;

IF @ApplicationId <= 0 OR NOT EXISTS (SELECT 1 FROM dbo.GNR_Applications WHERE Id = @ApplicationId)
    THROW 50302, N'WebsiteApplicationId must be an existing, positive GNR_Applications.Id.', 1;

IF SUSER_ID(@Login) IS NULL
    THROW 50303, N'WebsiteLoginName does not exist. Create it first (02-create-website-login.sql).', 1;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @Login AND type IN ('S', 'U', 'E'))
    THROW 50304, N'The website login must be an individual SQL, Windows or Entra login, not a group.', 1;

IF IS_SRVROLEMEMBER(N'sysadmin', @Login) = 1
   OR EXISTS (SELECT 1 FROM sys.server_role_members rm JOIN sys.server_principals sp ON sp.principal_id = rm.member_principal_id
              WHERE sp.name = @Login)
    THROW 50305, N'The website login holds a server role. Website identities must hold none.', 1;

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE sid = SUSER_SID(@Login) AND name <> @User)
    THROW 50306, N'The website login is already mapped to a different database user (possibly dbo).', 1;

IF SCHEMA_ID(N'ContentDeliverySecurity') IS NULL
   AND (DATABASE_PRINCIPAL_ID(N'ContentDeliveryWebsiteReader') IS NOT NULL
        OR EXISTS (SELECT 1 FROM sys.objects WHERE name = N'WebsiteTenantPolicy'))
    THROW 50307, N'Hardening object names already exist but were not created by this hardening.', 1;

IF SCHEMA_ID(N'ContentDeliverySecurity') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 3 AND name = N'ContentDeliveryHardening'
                   AND major_id = SCHEMA_ID(N'ContentDeliverySecurity'))
    THROW 50308, N'Schema ContentDeliverySecurity exists but was not created by this hardening.', 1;

IF DATABASE_PRINCIPAL_ID(@User) IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 4 AND name = N'ContentDeliveryHardening'
                   AND major_id = DATABASE_PRINCIPAL_ID(@User))
    THROW 50309, N'WebsiteUserName exists but was not created by this hardening. Use a new, dedicated user.', 1;

IF EXISTS (SELECT 1 FROM sys.security_predicates pr JOIN sys.security_policies sp ON sp.object_id = pr.object_id
           WHERE sp.schema_id <> ISNULL(SCHEMA_ID(N'ContentDeliverySecurity'), -1)
             AND pr.target_object_id IN (OBJECT_ID(N'dbo.CMS_Contents'), OBJECT_ID(N'dbo.CMS_ContentMetadata'),
                 OBJECT_ID(N'dbo.CMS_ContentSections'), OBJECT_ID(N'dbo.CMS_SectionElements'),
                 OBJECT_ID(N'dbo.CMS_ContentImages'), OBJECT_ID(N'dbo.CMS_Categories'), OBJECT_ID(N'dbo.GNR_Tags'),
                 OBJECT_ID(N'dbo.CMS_ContentInCategories'), OBJECT_ID(N'dbo.CMS_ContentInTags'),
                 OBJECT_ID(N'dbo.GNR_Cultures'), OBJECT_ID(N'dbo.CMS_ContentTranslations')))
    THROW 50310, N'Another security policy already targets a delivery table.', 1;

------------------------------------------------------------------------------------------------
-- Security schema (owned by dbo, like the CMS tables) and the protected mapping table.
------------------------------------------------------------------------------------------------
IF SCHEMA_ID(N'ContentDeliverySecurity') IS NULL
BEGIN
    EXEC (N'CREATE SCHEMA ContentDeliverySecurity AUTHORIZATION dbo;');
    EXEC sys.sp_addextendedproperty @name = N'ContentDeliveryHardening', @value = N'phase-6',
         @level0type = N'SCHEMA', @level0name = N'ContentDeliverySecurity';
END;

-- One row per website database user. PrincipalSid records the user's SID at mapping time so
-- verification can detect a dropped-and-recreated principal reusing the name.
IF OBJECT_ID(N'ContentDeliverySecurity.WebsitePrincipalApplication', N'U') IS NULL
    EXEC (N'CREATE TABLE ContentDeliverySecurity.WebsitePrincipalApplication (
        PrincipalName sysname        NOT NULL CONSTRAINT PK_WebsitePrincipalApplication PRIMARY KEY,
        PrincipalSid  varbinary(85)  NOT NULL,
        ApplicationId int            NOT NULL CONSTRAINT CK_WebsitePrincipalApplication_ApplicationId CHECK (ApplicationId > 0),
        MappedAtUtc   datetime2(0)   NOT NULL CONSTRAINT DF_WebsitePrincipalApplication_MappedAtUtc DEFAULT SYSUTCDATETIME(),
        MappedBy      sysname        NOT NULL CONSTRAINT DF_WebsitePrincipalApplication_MappedBy DEFAULT ORIGINAL_LOGIN());');

------------------------------------------------------------------------------------------------
-- Predicate functions: inline, schema-bound, two-part names, no type conversions.
-- CallerScope is the single place that decides who is restricted and to which application.
------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'ContentDeliverySecurity.fn_CallerScope') IS NULL
    EXEC (N'CREATE FUNCTION ContentDeliverySecurity.fn_CallerScope()
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT CAST(CASE WHEN m.PrincipalName IS NOT NULL
                       OR ISNULL(IS_ROLEMEMBER(N''ContentDeliveryWebsiteReader''), 0) = 1
                     THEN 1 ELSE 0 END AS bit) AS IsRestricted,
           m.ApplicationId
    FROM (SELECT 1 AS Caller) AS caller
    LEFT JOIN ContentDeliverySecurity.WebsitePrincipalApplication AS m ON m.PrincipalName = USER_NAME();');

-- CMS_Contents, CMS_Categories, GNR_Tags: rows owned by the caller's application.
IF OBJECT_ID(N'ContentDeliverySecurity.fn_TenantRowAccess') IS NULL
    EXEC (N'CREATE FUNCTION ContentDeliverySecurity.fn_TenantRowAccess(@ApplicationId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS AccessResult
    FROM ContentDeliverySecurity.fn_CallerScope() AS s
    WHERE s.IsRestricted = 0 OR s.ApplicationId = @ApplicationId;');

-- GNR_Cultures: restricted callers see global rows only; tenant-owned cultures are invisible,
-- including the caller's own (the SDK treats them as InvalidCulture anyway).
IF OBJECT_ID(N'ContentDeliverySecurity.fn_CultureRowAccess') IS NULL
    EXEC (N'CREATE FUNCTION ContentDeliverySecurity.fn_CultureRowAccess(@ApplicationId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS AccessResult
    FROM ContentDeliverySecurity.fn_CallerScope() AS s
    WHERE s.IsRestricted = 0 OR (s.ApplicationId IS NOT NULL AND @ApplicationId = 0);');

-- CMS_ContentMetadata, CMS_ContentSections, CMS_ContentImages, CMS_ContentTranslations.
IF OBJECT_ID(N'ContentDeliverySecurity.fn_ContentChildRowAccess') IS NULL
    EXEC (N'CREATE FUNCTION ContentDeliverySecurity.fn_ContentChildRowAccess(@ContentId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS AccessResult
    FROM ContentDeliverySecurity.fn_CallerScope() AS s
    WHERE s.IsRestricted = 0
       OR EXISTS (SELECT 1 FROM dbo.CMS_Contents AS c
                  WHERE c.Id = @ContentId AND c.ApplicationId = s.ApplicationId);');

-- CMS_SectionElements: owned through section -> content.
IF OBJECT_ID(N'ContentDeliverySecurity.fn_SectionChildRowAccess') IS NULL
    EXEC (N'CREATE FUNCTION ContentDeliverySecurity.fn_SectionChildRowAccess(@SectionId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS AccessResult
    FROM ContentDeliverySecurity.fn_CallerScope() AS s
    WHERE s.IsRestricted = 0
       OR EXISTS (SELECT 1 FROM dbo.CMS_ContentSections AS cs
                  JOIN dbo.CMS_Contents AS c ON c.Id = cs.ContentId
                  WHERE cs.Id = @SectionId AND c.ApplicationId = s.ApplicationId);');

-- Relation rows are visible only when both sides belong to the caller's application (rule T2).
IF OBJECT_ID(N'ContentDeliverySecurity.fn_ContentCategoryRowAccess') IS NULL
    EXEC (N'CREATE FUNCTION ContentDeliverySecurity.fn_ContentCategoryRowAccess(@ContentId int, @CategoryId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS AccessResult
    FROM ContentDeliverySecurity.fn_CallerScope() AS s
    WHERE s.IsRestricted = 0
       OR (EXISTS (SELECT 1 FROM dbo.CMS_Contents AS c WHERE c.Id = @ContentId AND c.ApplicationId = s.ApplicationId)
           AND EXISTS (SELECT 1 FROM dbo.CMS_Categories AS t WHERE t.Id = @CategoryId AND t.ApplicationId = s.ApplicationId));');

IF OBJECT_ID(N'ContentDeliverySecurity.fn_ContentTagRowAccess') IS NULL
    EXEC (N'CREATE FUNCTION ContentDeliverySecurity.fn_ContentTagRowAccess(@ContentId int, @TagId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS AccessResult
    FROM ContentDeliverySecurity.fn_CallerScope() AS s
    WHERE s.IsRestricted = 0
       OR (EXISTS (SELECT 1 FROM dbo.CMS_Contents AS c WHERE c.Id = @ContentId AND c.ApplicationId = s.ApplicationId)
           AND EXISTS (SELECT 1 FROM dbo.GNR_Tags AS t WHERE t.Id = @TagId AND t.ApplicationId = s.ApplicationId));');

------------------------------------------------------------------------------------------------
-- Security policy: filter predicates on every table the adapter reads. No block predicates: the
-- website role has no write permission at all (denied below).
------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'ContentDeliverySecurity.WebsiteTenantPolicy', N'SP') IS NULL
    EXEC (N'CREATE SECURITY POLICY ContentDeliverySecurity.WebsiteTenantPolicy
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_TenantRowAccess(ApplicationId) ON dbo.CMS_Contents,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_TenantRowAccess(ApplicationId) ON dbo.CMS_Categories,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_TenantRowAccess(ApplicationId) ON dbo.GNR_Tags,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_CultureRowAccess(ApplicationId) ON dbo.GNR_Cultures,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_ContentChildRowAccess(ContentId) ON dbo.CMS_ContentMetadata,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_ContentChildRowAccess(ContentId) ON dbo.CMS_ContentSections,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_ContentChildRowAccess(ContentId) ON dbo.CMS_ContentImages,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_ContentChildRowAccess(ContentId) ON dbo.CMS_ContentTranslations,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_SectionChildRowAccess(SectionId) ON dbo.CMS_SectionElements,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_ContentCategoryRowAccess(ContentId, CategoryId) ON dbo.CMS_ContentInCategories,
    ADD FILTER PREDICATE ContentDeliverySecurity.fn_ContentTagRowAccess(ContentId, TagId) ON dbo.CMS_ContentInTags
    WITH (STATE = ON, SCHEMABINDING = ON);');

------------------------------------------------------------------------------------------------
-- Website role: column-level SELECT on exactly the columns ContentDeliveryDbContext maps.
-- Never db_datareader, db_owner, or schema-wide SELECT.
------------------------------------------------------------------------------------------------
IF DATABASE_PRINCIPAL_ID(N'ContentDeliveryWebsiteReader') IS NULL
    EXEC (N'CREATE ROLE ContentDeliveryWebsiteReader AUTHORIZATION dbo;');

GRANT SELECT ON dbo.CMS_Contents ([Id], [ApplicationId], [TypeId], [Title], [HeadLine], [Abstract], [Description],
    [PublishDt], [FarsiContent], [IsActive], [IsDeleted], [UpdatedDT]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.CMS_ContentMetadata ([Id], [ContentId], [Title], [Author], [Keywords], [Description],
    [IsDeleted]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.CMS_ContentSections ([Id], [ContentId], [Priority], [IsActive], [IsDeleted]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.CMS_SectionElements ([Id], [SectionId], [ElementType], [ElementTitle], [TinyText], [EditorText],
    [FileNameText], [GalleryImages], [Size], [IsActive], [IsDeleted]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.CMS_ContentImages ([Id], [ContentId], [ImageFileName], [Size], [IsActive], [IsDeleted]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.CMS_Categories ([Id], [ApplicationId], [ParentId], [Title], [Description], [IsActive],
    [IsDeleted]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.GNR_Tags ([Id], [ApplicationId], [Title], [IsActive], [IsDeleted]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.CMS_ContentInCategories ([Id], [ContentId], [CategoryId]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.CMS_ContentInTags ([Id], [ContentId], [TagId]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.GNR_Cultures ([Id], [ApplicationId], [Key], [IsActive], [IsDeleted]) TO ContentDeliveryWebsiteReader;
GRANT SELECT ON dbo.CMS_ContentTranslations ([Id], [ContentId], [CultureId], [TranslationStatus], [SourceFingerprint],
    [LocalizedTextJson], [IsDeleted], [UpdatedDT]) TO ContentDeliveryWebsiteReader;

-- No writes, no DDL, no procedure execution, no security-policy or principal changes.
DENY INSERT, UPDATE, DELETE, EXECUTE, ALTER, CONTROL, TAKE OWNERSHIP, REFERENCES ON SCHEMA::dbo TO ContentDeliveryWebsiteReader;
DENY ALTER ANY SECURITY POLICY, ALTER ANY SCHEMA, ALTER ANY ROLE, ALTER ANY USER, ALTER ANY APPLICATION ROLE,
     CREATE TABLE, CREATE VIEW, CREATE FUNCTION, CREATE PROCEDURE, CREATE SCHEMA, SHOWPLAN TO ContentDeliveryWebsiteReader;

-- The mapping table and predicate functions are never directly accessible to websites.
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE, REFERENCES, ALTER, CONTROL, TAKE OWNERSHIP, VIEW DEFINITION
    ON SCHEMA::ContentDeliverySecurity TO ContentDeliveryWebsiteReader;

-- Every other user table, view and function (CMS, security, BackOffice, job tables...) is denied
-- explicitly so a public/guest grant cannot leak into the website. Objects created after this
-- deployment are not covered: re-run this script after schema changes (README section 7).
SET @Sql = N'';
SELECT @Sql += CASE WHEN o.type IN ('P', 'PC', 'FN', 'FS') THEN N'DENY EXECUTE ON ' ELSE N'DENY SELECT ON ' END
    + QUOTENAME(SCHEMA_NAME(o.schema_id)) + N'.' + QUOTENAME(o.name) + N' TO ContentDeliveryWebsiteReader;' + NCHAR(10)
FROM sys.objects o
WHERE o.is_ms_shipped = 0
  AND o.type IN ('U', 'V', 'P', 'PC', 'FN', 'FS', 'IF', 'TF', 'FT')
  AND o.schema_id <> SCHEMA_ID(N'ContentDeliverySecurity')
  AND o.object_id NOT IN (OBJECT_ID(N'dbo.CMS_Contents'), OBJECT_ID(N'dbo.CMS_ContentMetadata'),
      OBJECT_ID(N'dbo.CMS_ContentSections'), OBJECT_ID(N'dbo.CMS_SectionElements'), OBJECT_ID(N'dbo.CMS_ContentImages'),
      OBJECT_ID(N'dbo.CMS_Categories'), OBJECT_ID(N'dbo.GNR_Tags'), OBJECT_ID(N'dbo.CMS_ContentInCategories'),
      OBJECT_ID(N'dbo.CMS_ContentInTags'), OBJECT_ID(N'dbo.GNR_Cultures'), OBJECT_ID(N'dbo.CMS_ContentTranslations'));
EXEC sys.sp_executesql @Sql;

------------------------------------------------------------------------------------------------
-- Website user: dedicated, tagged, member of the website role only, mapped to one application.
------------------------------------------------------------------------------------------------
IF DATABASE_PRINCIPAL_ID(@User) IS NULL
BEGIN
    SET @Sql = N'CREATE USER ' + QUOTENAME(@User) + N' FOR LOGIN ' + QUOTENAME(@Login) + N' WITH DEFAULT_SCHEMA = dbo;';
    EXEC sys.sp_executesql @Sql;
    EXEC sys.sp_addextendedproperty @name = N'ContentDeliveryHardening', @value = N'phase-6',
         @level0type = N'USER', @level0name = @User;
END;

IF ISNULL(IS_ROLEMEMBER(N'ContentDeliveryWebsiteReader', @User), 0) = 0
BEGIN
    SET @Sql = N'ALTER ROLE ContentDeliveryWebsiteReader ADD MEMBER ' + QUOTENAME(@User) + N';';
    EXEC sys.sp_executesql @Sql;
END;

IF EXISTS (SELECT 1 FROM sys.database_role_members rm
           JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
           WHERE rm.member_principal_id = DATABASE_PRINCIPAL_ID(@User) AND r.name <> N'ContentDeliveryWebsiteReader')
    THROW 50311, N'The website user is a member of another database role. Remove it before deploying.', 1;

IF EXISTS (SELECT 1 FROM sys.database_permissions WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(@User)
           AND permission_name <> N'CONNECT' AND state IN ('G', 'W'))
    THROW 50312, N'The website user holds direct grants. Website permissions come from the role only.', 1;

-- Map the user to its application. Re-pointing an existing mapping is refused: move a website
-- to another application only through a reviewed change (README section 9).
IF EXISTS (SELECT 1 FROM ContentDeliverySecurity.WebsitePrincipalApplication
           WHERE PrincipalName = @User AND ApplicationId <> @ApplicationId)
    THROW 50313, N'The website user is already mapped to a different application.', 1;

IF NOT EXISTS (SELECT 1 FROM ContentDeliverySecurity.WebsitePrincipalApplication WHERE PrincipalName = @User)
    INSERT INTO ContentDeliverySecurity.WebsitePrincipalApplication (PrincipalName, PrincipalSid, ApplicationId)
    SELECT name, sid, @ApplicationId FROM sys.database_principals WHERE name = @User;

COMMIT TRANSACTION;

-- Deployment summary (no content, no credentials).
SELECT N'deployed' AS [Check],
       (SELECT is_enabled FROM sys.security_policies WHERE object_id = OBJECT_ID(N'ContentDeliverySecurity.WebsiteTenantPolicy')) AS PolicyEnabled,
       (SELECT COUNT(*) FROM sys.security_predicates WHERE object_id = OBJECT_ID(N'ContentDeliverySecurity.WebsiteTenantPolicy')) AS PredicateCount,
       (SELECT COUNT(*) FROM ContentDeliverySecurity.WebsitePrincipalApplication) AS MappedWebsiteUsers,
       CASE WHEN (SELECT COUNT(*) FROM sys.security_predicates
                  WHERE object_id = OBJECT_ID(N'ContentDeliverySecurity.WebsiteTenantPolicy')) = 11
            THEN N'OK: run 04-verify-restored-backup.sql next' ELSE N'FAIL: expected 11 predicates' END AS Verdict;
