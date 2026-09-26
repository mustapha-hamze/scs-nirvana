-- Content Delivery database hardening (Phase 6) - 02 check the already-provisioned website login
-- Companion to README.md in this folder (sections 3 and 8).
--
-- READ-ONLY. This repository never creates logins or handles credentials. The DBA provisions
-- the dedicated website login - SQL auth, or an individual Windows/gMSA or Entra login - through
-- the approved DBA/secret-management process, outside these scripts, then runs this check and
-- acknowledges that out-of-band provisioning. It returns no secret, hash or connection string.
--
-- Usage:
--   sqlcmd -S <server> -E -b -i 02-check-website-login.sql \
--     -v CmsDatabase="<db>" WebsiteLoginName="<login>" CredentialsProvisionedOutOfBand=CONFIRMED

:on error exit
SET NOCOUNT ON;
USE [$(CmsDatabase)];

DECLARE @Login sysname = N'$(WebsiteLoginName)';

IF N'$(CredentialsProvisionedOutOfBand)' <> N'CONFIRMED'
    THROW 50201, N'Provision the login through the approved DBA process first, then set CredentialsProvisionedOutOfBand=CONFIRMED.', 1;

IF SUSER_ID(@Login) IS NULL
    THROW 50202, N'WebsiteLoginName does not exist. Provision it through the approved DBA process.', 1;

SELECT N'website login' AS [Check], sp.type_desc, sp.is_disabled, sp.default_database_name,
       sl.is_policy_checked,
       (SELECT COUNT(*) FROM sys.server_role_members rm WHERE rm.member_principal_id = sp.principal_id) AS ServerRoleCount,
       (SELECT COUNT(*) FROM sys.server_permissions pe WHERE pe.grantee_principal_id = sp.principal_id
            AND pe.permission_name <> N'CONNECT SQL') AS ExtraServerPermissionCount,
       (SELECT COUNT(*) FROM sys.database_principals dp WHERE dp.sid = sp.sid) AS UsersInThisDatabase,
       CASE WHEN sp.type NOT IN ('S', 'U', 'E') THEN N'BLOCKER: must be an individual login, not a group'
            WHEN sp.is_disabled = 1 THEN N'BLOCKER: disabled'
            WHEN IS_SRVROLEMEMBER(N'sysadmin', sp.name) = 1
              OR EXISTS (SELECT 1 FROM sys.server_role_members rm WHERE rm.member_principal_id = sp.principal_id)
                THEN N'BLOCKER: holds a server role'
            WHEN EXISTS (SELECT 1 FROM sys.server_permissions pe WHERE pe.grantee_principal_id = sp.principal_id
                         AND pe.permission_name <> N'CONNECT SQL') THEN N'BLOCKER: extra server permissions'
            WHEN sp.type = 'S' AND sl.is_policy_checked = 0 THEN N'REVIEW: SQL login without password policy'
            WHEN EXISTS (SELECT 1 FROM sys.database_principals dp WHERE dp.sid = sp.sid
                         AND NOT EXISTS (SELECT 1 FROM sys.extended_properties ep WHERE ep.class = 4
                                         AND ep.major_id = dp.principal_id AND ep.name = N'ContentDeliveryHardening'))
                THEN N'BLOCKER: already mapped to a user this hardening did not create'
            ELSE N'OK: run 03-deploy.sql' END AS Verdict
FROM sys.server_principals sp
LEFT JOIN sys.sql_logins sl ON sl.principal_id = sp.principal_id
WHERE sp.name = @Login;
