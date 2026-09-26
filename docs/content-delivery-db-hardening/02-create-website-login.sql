-- Content Delivery database hardening (Phase 6) - 02 create the website's SQL-auth login
-- Companion to README.md in this folder. Skip this script when the website uses an existing
-- individual Windows/gMSA or Microsoft Entra login (never a group login).
--
-- Server-level and idempotent: creates the login only if it does not exist and never changes an
-- existing login or its password. The login gets no server role and no database access here;
-- 03-deploy.sql creates its database user.
--
-- The password comes from the secret store through an environment variable, never from the
-- command line or this file (sqlcmd reads environment variables as scripting variables):
--   export WebsiteLoginPassword="$(<secret-store read command>)"
--   sqlcmd -S <server> -E -b -i 02-create-website-login.sql \
--     -v CmsDatabase="<db>" WebsiteLoginName="<login>"
--   unset WebsiteLoginPassword

:on error exit
SET NOCOUNT ON;

DECLARE @Login sysname = N'$(WebsiteLoginName)';

IF DB_ID(N'$(CmsDatabase)') IS NULL
    THROW 50201, N'CmsDatabase does not exist on this server.', 1;

IF SUSER_ID(@Login) IS NOT NULL
BEGIN
    PRINT N'Login already exists; left unchanged (password not modified).';
    RETURN;
END;

IF LEN(N'$(WebsiteLoginPassword)') < 24
    THROW 50202, N'WebsiteLoginPassword must be a generated secret of at least 24 characters.', 1;

-- CHECK_POLICY enforces the Windows password policy; expiration is off because a service
-- identity is rotated deliberately (README section 8), not by expiry.
DECLARE @Sql nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME(@Login)
    + N' WITH PASSWORD = N''$(WebsiteLoginPassword)'', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = '
    + QUOTENAME(N'$(CmsDatabase)') + N';';
EXEC sys.sp_executesql @Sql;

PRINT N'Login created. Record in the change ticket that this hardening created it (needed for rollback).';
