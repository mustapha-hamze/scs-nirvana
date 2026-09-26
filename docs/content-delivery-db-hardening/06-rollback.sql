-- Content Delivery database hardening (Phase 6) - 06 rollback
-- Companion to README.md in this folder (section 9).
--
-- Removes ONLY objects this hardening created, identified by the ContentDeliveryHardening
-- extended property (schema, users) and the dedicated role/schema names. It deletes no CMS row and
-- alters no CMS table. Idempotent: running it when nothing is deployed changes nothing.
--
-- Order: website access is removed first (users, then role), so no website identity is ever
-- left with SELECT on an unfiltered table; then the policy (which unbinds the functions), the
-- functions, the mapping table and the schema. Switch websites back to their previous
-- connection before running it - the hardened website users stop working.
--
-- Server logins are not dropped here; see README section 9 for the separate, manual step.
--
-- Usage:
--   sqlcmd -S <server> -E -b -i 06-rollback.sql -v CmsDatabase="<db>" ConfirmRollback=ROLLBACK

:on error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
USE [$(CmsDatabase)];

IF N'$(ConfirmRollback)' <> N'ROLLBACK'
    THROW 50600, N'Set ConfirmRollback=ROLLBACK to remove the content delivery hardening.', 1;

IF SCHEMA_ID(N'ContentDeliverySecurity') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 3 AND name = N'ContentDeliveryHardening'
                   AND major_id = SCHEMA_ID(N'ContentDeliverySecurity'))
    THROW 50601, N'Schema ContentDeliverySecurity was not created by this hardening; nothing removed.', 1;

DECLARE @Sql nvarchar(max) = N'';

BEGIN TRANSACTION;

-- 1) Hardening-created website users: leave the role, then drop.
SELECT @Sql += N'ALTER ROLE ContentDeliveryWebsiteReader DROP MEMBER ' + QUOTENAME(p.name) + N';' + NCHAR(10)
FROM sys.database_principals p
JOIN sys.extended_properties ep ON ep.class = 4 AND ep.major_id = p.principal_id AND ep.name = N'ContentDeliveryHardening'
WHERE ISNULL(IS_ROLEMEMBER(N'ContentDeliveryWebsiteReader', p.name), 0) = 1;
SELECT @Sql += N'DROP USER ' + QUOTENAME(p.name) + N';' + NCHAR(10)
FROM sys.database_principals p
JOIN sys.extended_properties ep ON ep.class = 4 AND ep.major_id = p.principal_id AND ep.name = N'ContentDeliveryHardening';
EXEC sys.sp_executesql @Sql;

-- 2) Website role (its GRANTs and DENYs disappear with it). Refuse if someone else joined it.
IF DATABASE_PRINCIPAL_ID(N'ContentDeliveryWebsiteReader') IS NOT NULL AND SCHEMA_ID(N'ContentDeliverySecurity') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id = DATABASE_PRINCIPAL_ID(N'ContentDeliveryWebsiteReader'))
        THROW 50602, N'ContentDeliveryWebsiteReader still has members not created by this hardening. Review them first.', 1;
    DROP ROLE ContentDeliveryWebsiteReader;
END;

-- 3) Security policy, then the functions it bound (dependants first), then table and schema.
IF OBJECT_ID(N'ContentDeliverySecurity.WebsiteTenantPolicy', N'SP') IS NOT NULL
    DROP SECURITY POLICY ContentDeliverySecurity.WebsiteTenantPolicy;
IF OBJECT_ID(N'ContentDeliverySecurity.fn_ContentTagRowAccess') IS NOT NULL DROP FUNCTION ContentDeliverySecurity.fn_ContentTagRowAccess;
IF OBJECT_ID(N'ContentDeliverySecurity.fn_ContentCategoryRowAccess') IS NOT NULL DROP FUNCTION ContentDeliverySecurity.fn_ContentCategoryRowAccess;
IF OBJECT_ID(N'ContentDeliverySecurity.fn_SectionChildRowAccess') IS NOT NULL DROP FUNCTION ContentDeliverySecurity.fn_SectionChildRowAccess;
IF OBJECT_ID(N'ContentDeliverySecurity.fn_ContentChildRowAccess') IS NOT NULL DROP FUNCTION ContentDeliverySecurity.fn_ContentChildRowAccess;
IF OBJECT_ID(N'ContentDeliverySecurity.fn_CultureRowAccess') IS NOT NULL DROP FUNCTION ContentDeliverySecurity.fn_CultureRowAccess;
IF OBJECT_ID(N'ContentDeliverySecurity.fn_TenantRowAccess') IS NOT NULL DROP FUNCTION ContentDeliverySecurity.fn_TenantRowAccess;
IF OBJECT_ID(N'ContentDeliverySecurity.fn_CallerScope') IS NOT NULL DROP FUNCTION ContentDeliverySecurity.fn_CallerScope;
IF OBJECT_ID(N'ContentDeliverySecurity.WebsitePrincipalApplication', N'U') IS NOT NULL DROP TABLE ContentDeliverySecurity.WebsitePrincipalApplication;

IF EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N'ContentDeliverySecurity'))
    THROW 50603, N'ContentDeliverySecurity contains objects this hardening did not create. Review them first.', 1;
IF SCHEMA_ID(N'ContentDeliverySecurity') IS NOT NULL
    DROP SCHEMA ContentDeliverySecurity;

COMMIT TRANSACTION;

SELECT N'rolled back' AS [Check],
       CASE WHEN SCHEMA_ID(N'ContentDeliverySecurity') IS NULL
             AND DATABASE_PRINCIPAL_ID(N'ContentDeliveryWebsiteReader') IS NULL
             AND NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 4 AND name = N'ContentDeliveryHardening')
            THEN N'OK: no hardening object remains' ELSE N'FAIL: objects remain' END AS Verdict;
