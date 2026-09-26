-- Content Delivery database hardening (Phase 6) - 05 direct check as the real website login
-- Companion to README.md in this folder (section 5). Complements 04 (which impersonates the login
-- inside a rolled-back transaction): this one connects with the website's own credentials, the
-- way the SDK does, so no impersonation is involved.
--
-- Runs as the website login; read-only for that identity. Any probe that unexpectedly succeeds
-- is rolled back. Returns counts, error numbers and verdicts only.
--
-- Usage: connect as the website login through the approved DBA credential process. For SQL auth,
-- give -U without -P so sqlcmd prompts interactively; the secret never appears in this script,
-- a command line, a variable or the output. Windows/gMSA/Entra logins use their own sign-in.
--   sqlcmd -S <server> -U "<website login>" -d "<db>" -b -i 05-verify-website-connection.sql \
--     -v ExpectedApplicationId=<id>
-- Run it a second time with a different workstation name (-H spoofed-host) and confirm identical
-- results: nothing about the connection other than the authenticated identity may matter.

:on error exit
SET NOCOUNT ON;
SET XACT_ABORT OFF;

DECLARE @A int = $(ExpectedApplicationId);
DECLARE @Checks TABLE (Seq int IDENTITY, CheckName nvarchar(200), Observed nvarchar(400), Verdict nvarchar(20));
DECLARE @Probes TABLE (Seq int IDENTITY, CheckName nvarchar(200), ProbeSql nvarchar(max), ExpectDenied bit);
DECLARE @CheckName nvarchar(200), @ProbeSql nvarchar(max), @ExpectDenied bit, @Err int, @N int, @Before int, @Ci varbinary(128);

INSERT INTO @Checks VALUES (N'identity is a restricted website user',
    CONCAT(N'role member=', ISNULL(IS_ROLEMEMBER(N'ContentDeliveryWebsiteReader'), -1),
           N'; db_owner=', ISNULL(IS_MEMBER(N'db_owner'), -1), N'; sysadmin=', ISNULL(IS_SRVROLEMEMBER(N'sysadmin'), -1)),
    IIF(IS_ROLEMEMBER(N'ContentDeliveryWebsiteReader') = 1 AND IS_MEMBER(N'db_owner') = 0 AND IS_SRVROLEMEMBER(N'sysadmin') = 0,
        N'PASS', N'FAIL'));

-- Visible rows (plain SQL, no SDK). Only the expected application and global cultures may appear.
SELECT @N = (SELECT COUNT(*) FROM dbo.CMS_Contents WHERE ApplicationId <> @A)
          + (SELECT COUNT(*) FROM dbo.CMS_Categories WHERE ApplicationId <> @A)
          + (SELECT COUNT(*) FROM dbo.GNR_Tags WHERE ApplicationId <> @A)
          + (SELECT COUNT(*) FROM dbo.GNR_Cultures WHERE ApplicationId <> 0);
INSERT INTO @Checks VALUES (N'foreign tenant or tenant-owned culture rows visible', CONCAT(@N, N' rows'), IIF(@N = 0, N'PASS', N'FAIL'));

SET @Before = (SELECT COUNT(*) FROM dbo.CMS_Contents);
INSERT INTO @Checks VALUES (N'own content rows visible', CONCAT(@Before, N' rows'), IIF(@Before > 0, N'PASS', N'REVIEW'));

SET @N = (SELECT COUNT(*) FROM dbo.GNR_Cultures WHERE ApplicationId = 0 AND IsActive = 1 AND IsDeleted = 0);
INSERT INTO @Checks VALUES (N'active global cultures readable', CONCAT(@N, N' rows'), IIF(@N > 0, N'PASS', N'FAIL'));

-- Descendants whose owning content is not visible would mean a broken ownership chain.
SET @N = (SELECT COUNT(*) FROM dbo.CMS_ContentSections s WHERE NOT EXISTS (SELECT 1 FROM dbo.CMS_Contents c WHERE c.Id = s.ContentId))
       + (SELECT COUNT(*) FROM dbo.CMS_ContentMetadata m WHERE NOT EXISTS (SELECT 1 FROM dbo.CMS_Contents c WHERE c.Id = m.ContentId))
       + (SELECT COUNT(*) FROM dbo.CMS_ContentImages i WHERE NOT EXISTS (SELECT 1 FROM dbo.CMS_Contents c WHERE c.Id = i.ContentId))
       + (SELECT COUNT(*) FROM dbo.CMS_ContentTranslations t WHERE NOT EXISTS (SELECT 1 FROM dbo.CMS_Contents c WHERE c.Id = t.ContentId))
       + (SELECT COUNT(*) FROM dbo.CMS_SectionElements e WHERE NOT EXISTS (SELECT 1 FROM dbo.CMS_ContentSections s WHERE s.Id = e.SectionId))
       + (SELECT COUNT(*) FROM dbo.CMS_ContentInCategories r WHERE NOT EXISTS (SELECT 1 FROM dbo.CMS_Categories t WHERE t.Id = r.CategoryId)
                                                             OR NOT EXISTS (SELECT 1 FROM dbo.CMS_Contents c WHERE c.Id = r.ContentId))
       + (SELECT COUNT(*) FROM dbo.CMS_ContentInTags r WHERE NOT EXISTS (SELECT 1 FROM dbo.GNR_Tags t WHERE t.Id = r.TagId)
                                                       OR NOT EXISTS (SELECT 1 FROM dbo.CMS_Contents c WHERE c.Id = r.ContentId));
INSERT INTO @Checks VALUES (N'visible descendants/relations without a visible owner', CONCAT(@N, N' rows'), IIF(@N = 0, N'PASS', N'FAIL'));

-- Spoofing through caller-controlled session state.
EXEC sys.sp_set_session_context @key = N'ApplicationId', @value = -1;
EXEC sys.sp_set_session_context @key = N'TenantId', @value = -1;
SET @Ci = CAST(-1 AS varbinary(128));
SET CONTEXT_INFO @Ci;
SET @N = (SELECT COUNT(*) FROM dbo.CMS_Contents);
EXEC sys.sp_set_session_context @key = N'ApplicationId', @value = NULL;
EXEC sys.sp_set_session_context @key = N'TenantId', @value = NULL;
SET CONTEXT_INFO 0x;
INSERT INTO @Checks VALUES (N'spoof: SESSION_CONTEXT and CONTEXT_INFO changed', CONCAT(@N, N' contents visible (baseline ', @Before, N')'),
    IIF(@N = @Before, N'PASS', N'FAIL'));

INSERT INTO @Probes (CheckName, ProbeSql, ExpectDenied) VALUES
    (N'write: UPDATE CMS_Contents', N'UPDATE dbo.CMS_Contents SET IsActive = IsActive WHERE 1 = 0;', 1),
    (N'write: DELETE CMS_ContentTranslations', N'DELETE FROM dbo.CMS_ContentTranslations WHERE 1 = 0;', 1),
    (N'ddl: CREATE TABLE', N'CREATE TABLE dbo.cd_rls_probe_table (Id int);', 1),
    (N'mapping table: SELECT', N'DECLARE @n int; SELECT @n = COUNT(*) FROM ContentDeliverySecurity.WebsitePrincipalApplication;', 1),
    (N'policy: disable', N'ALTER SECURITY POLICY ContentDeliverySecurity.WebsiteTenantPolicy WITH (STATE = OFF);', 1),
    (N'impersonate dbo', N'EXECUTE AS USER = ''dbo''; REVERT;', 1),
    (N'unrelated table: GNR_Applications', N'DECLARE @n int; SELECT @n = COUNT(*) FROM dbo.GNR_Applications;', 1),
    (N'unmapped column: CMS_Contents.CreatedDT', N'DECLARE @d datetime2; SELECT @d = MAX(CreatedDT) FROM dbo.CMS_Contents;', 1),
    (N'sdk shape: COUNT(*)', N'DECLARE @n int; SELECT @n = COUNT(*) FROM dbo.CMS_Contents WHERE IsActive = 1 AND IsDeleted = 0;', 0),
    (N'sdk shape: EXISTS on relation', N'DECLARE @n int = 0; IF EXISTS (SELECT 1 FROM dbo.CMS_ContentInCategories) SET @n = 1;', 0);

BEGIN TRANSACTION; -- anything that unexpectedly succeeds is undone below
DECLARE probes CURSOR LOCAL FAST_FORWARD FOR SELECT CheckName, ProbeSql, ExpectDenied FROM @Probes ORDER BY Seq;
OPEN probes;
FETCH NEXT FROM probes INTO @CheckName, @ProbeSql, @ExpectDenied;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Err = 0;
    BEGIN TRY
        EXEC sys.sp_executesql @ProbeSql;
    END TRY
    BEGIN CATCH
        SET @Err = ERROR_NUMBER();
    END CATCH;
    INSERT INTO @Checks VALUES (@CheckName, IIF(@Err = 0, N'succeeded', CONCAT(N'error ', @Err)),
        CASE WHEN @ExpectDenied = 0 THEN IIF(@Err = 0, N'PASS', N'FAIL')
             WHEN @Err = 0 THEN N'FAIL'
             WHEN @Err IN (229, 230, 262, 297, 300, 15151, 15247, 15517) THEN N'PASS'
             ELSE N'REVIEW' END);
    FETCH NEXT FROM probes INTO @CheckName, @ProbeSql, @ExpectDenied;
END;
CLOSE probes;
DEALLOCATE probes;
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

SELECT CheckName, Observed, Verdict FROM @Checks ORDER BY Seq;
SELECT CASE WHEN EXISTS (SELECT 1 FROM @Checks WHERE Verdict = N'FAIL') THEN N'FAIL'
            WHEN EXISTS (SELECT 1 FROM @Checks WHERE Verdict = N'REVIEW') THEN N'REVIEW'
            ELSE N'PASS' END AS OverallVerdict,
       SUSER_SNAME() AS LoginName, USER_NAME() AS DatabaseUser, DB_NAME() AS DatabaseName, SYSUTCDATETIME() AS CheckedAtUtc;
