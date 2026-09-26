-- Content Delivery database hardening (Phase 6) - 04 isolation verification on a RESTORED BACKUP
-- Companion to README.md in this folder (section 5). Run after 03-deploy.sql on the restored copy.
--
-- NEVER run on production. Everything happens inside one transaction that is ALWAYS rolled back:
-- seeded test rows, probe users and probe mappings disappear at the end. The script returns
-- counts, error numbers and verdicts only - no content text, titles, keys or row ids.
--
-- What it proves (as the website principal, bypassing the SDK, with plain SQL):
--   * every delivery table returns exactly the rows of the mapped application and its
--     descendants (no leaked row, no missing row), relations only when both sides are owned,
--     global cultures only (ApplicationId = 0), tenant-owned cultures invisible;
--   * a principal mapped to the second application sees none of the first application's rows;
--   * a role member without a mapping row sees nothing (fail-closed);
--   * writes, the mapping table, predicate functions, unrelated tables, unmapped columns,
--     policy changes and impersonation are denied;
--   * setting SESSION_CONTEXT / CONTEXT_INFO to another application changes nothing.
--
-- Isolation needs a second tenant. Production may contain only one, so either
--   SeedTestRows=1 SecondApplicationId=0   clone a temporary second application (+ content,
--                                          descendants, taxonomy, relations, cultures) inside
--                                          the rolled-back transaction (DBA-approved), or
--   SeedTestRows=0 SecondApplicationId=<id> use an existing second tenant as it is, or
--   SeedTestRows=1 SecondApplicationId=<id> seed extra cross-tenant rows under an existing one.
--
-- Usage (sysadmin on the restored copy; EXECUTE AS LOGIN keeps one session and one transaction):
--   sqlcmd -S <restore-server> -E -b -i 04-verify-restored-backup.sql \
--     -v CmsDatabase="<restored db>" WebsiteLoginName="<login>" WebsiteApplicationId=<id> \
--        SecondApplicationId=0 SeedTestRows=1 ConfirmRestoredCopy=RESTORED_COPY

:on error exit
SET NOCOUNT ON;
USE [$(CmsDatabase)];

IF N'$(ConfirmRestoredCopy)' <> N'RESTORED_COPY'
    THROW 50400, N'Set ConfirmRestoredCopy=RESTORED_COPY. This script must only run on a restored backup.', 1;
GO

-- Clones one row of dbo.<Table>, overriding the columns listed in #Override. Copies stay inside
-- the verification transaction and are rolled back; nothing is returned to the client.
CREATE PROCEDURE #CloneRow @Table sysname, @SourceId int, @NewId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Cols nvarchar(max) = N'', @Vals nvarchar(max) = N'', @Sql nvarchar(max);

    SELECT @Cols += N', ' + QUOTENAME(c.name),
           @Vals += N', ' + COALESCE(o.ValueSql,
                CASE WHEN c.name = N'Id' THEN N'(SELECT MAX(x.Id) + 1 FROM dbo.' + QUOTENAME(@Table) + N' AS x)'
                     ELSE N's.' + QUOTENAME(c.name) END)
    FROM sys.columns c
    LEFT JOIN #Override o ON o.ColumnName = c.name
    WHERE c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@Table))
      AND c.is_identity = 0 AND c.is_computed = 0 AND c.system_type_id <> 189; -- rowversion

    SET @Sql = N'INSERT INTO dbo.' + QUOTENAME(@Table) + N' (' + STUFF(@Cols, 1, 2, N'')
             + N') OUTPUT inserted.Id INTO #NewId (Id) SELECT ' + STUFF(@Vals, 1, 2, N'')
             + N' FROM dbo.' + QUOTENAME(@Table) + N' AS s WHERE s.Id = @SourceId;';

    DELETE FROM #NewId;
    EXEC sys.sp_executesql @Sql, N'@SourceId int', @SourceId = @SourceId;
    SET @NewId = (SELECT MAX(Id) FROM #NewId);
    DELETE FROM #Override;
    IF @NewId IS NULL
        THROW 50401, N'Seed clone inserted no row.', 1;
END;
GO

SET NOCOUNT ON;
SET XACT_ABORT OFF; -- expected permission errors must not abort the verification transaction

DECLARE @Login sysname = N'$(WebsiteLoginName)';
DECLARE @A int = $(WebsiteApplicationId);
DECLARE @B int = $(SecondApplicationId);
DECLARE @Seed bit = $(SeedTestRows);
DECLARE @WebsiteUser sysname = (SELECT name FROM sys.database_principals WHERE sid = SUSER_SID(@Login));

IF @WebsiteUser IS NULL
    THROW 50402, N'The website login has no user in this database. Run 03-deploy.sql first.', 1;
IF NOT EXISTS (SELECT 1 FROM ContentDeliverySecurity.WebsitePrincipalApplication WHERE PrincipalName = @WebsiteUser AND ApplicationId = @A)
    THROW 50403, N'The website user is not mapped to WebsiteApplicationId.', 1;
IF @Seed = 0 AND (@B <= 0 OR NOT EXISTS (SELECT 1 FROM dbo.GNR_Applications WHERE Id = @B))
    THROW 50404, N'Without seeding, SecondApplicationId must be an existing application.', 1;
IF @B = @A
    THROW 50405, N'SecondApplicationId must differ from WebsiteApplicationId.', 1;

DECLARE @Principals TABLE (Label sysname PRIMARY KEY, Kind char(5) NOT NULL, Name sysname NOT NULL, ApplicationId int NULL);
DECLARE @Visible TABLE (Label sysname, TableName sysname, RowId int, PRIMARY KEY (Label, TableName, RowId));
DECLARE @Allowed TABLE (Label sysname, TableName sysname, RowId int, PRIMARY KEY (Label, TableName, RowId));
DECLARE @Totals TABLE (TableName sysname PRIMARY KEY, TotalRows int);
DECLARE @Results TABLE (Label sysname, TableName sysname, AllowedRows int, VisibleRows int, LeakedRows int,
                        MissingRows int, ForeignRows int, Verdict nvarchar(20));
DECLARE @Checks TABLE (Seq int IDENTITY, Label sysname, CheckName nvarchar(200), Observed nvarchar(400), Verdict nvarchar(20));
DECLARE @Probes TABLE (Seq int IDENTITY, CheckName nvarchar(200), ProbeSql nvarchar(max), ExpectDenied bit);

DECLARE @Label sysname, @Kind char(5), @Name sysname, @Seq int, @CheckName nvarchar(200), @ProbeSql nvarchar(max),
        @ExpectDenied bit, @Err int, @N int, @Ci varbinary(128), @ClonedApplication bit = 0,
        @Src int, @Tmp int, @ContentB int, @SectionSrc int, @SectionB int, @CatA int, @CatB int, @TagA int, @TagB int;

CREATE TABLE #Override (ColumnName sysname PRIMARY KEY, ValueSql nvarchar(400) NOT NULL);
CREATE TABLE #NewId (Id int);

BEGIN TRANSACTION;
BEGIN TRY
    INSERT INTO @Checks (Label, CheckName, Observed, Verdict)
    SELECT N'website', N'mapping SID matches the database user',
           CASE WHEN m.PrincipalSid = p.sid THEN N'match' ELSE N'mismatch' END,
           CASE WHEN m.PrincipalSid = p.sid THEN N'PASS' ELSE N'FAIL' END
    FROM ContentDeliverySecurity.WebsitePrincipalApplication m
    JOIN sys.database_principals p ON p.name = m.PrincipalName
    WHERE m.PrincipalName = @WebsiteUser;

    -------------------------------------------------------------------------------------------
    -- Seed a second tenant (rolled back). Each clone copies one existing row of application A.
    -------------------------------------------------------------------------------------------
    IF @Seed = 1
    BEGIN
        IF @B <= 0
        BEGIN
            INSERT INTO #Override VALUES (N'ApplicationKey', N'CONCAT(N''cd-rls-'', CONVERT(nvarchar(36), NEWID()))');
            EXEC #CloneRow N'GNR_Applications', @A, @B OUTPUT;
            SET @ClonedApplication = 1;
        END;

        -- Source content: prefer one with translations, elements, images and metadata.
        SELECT TOP (1) @Src = c.Id FROM dbo.CMS_Contents c WHERE c.ApplicationId = @A
        ORDER BY CASE WHEN EXISTS (SELECT 1 FROM dbo.CMS_ContentTranslations t WHERE t.ContentId = c.Id) THEN 0 ELSE 1 END,
                 CASE WHEN EXISTS (SELECT 1 FROM dbo.CMS_ContentSections s JOIN dbo.CMS_SectionElements e ON e.SectionId = s.Id
                                   WHERE s.ContentId = c.Id) THEN 0 ELSE 1 END,
                 CASE WHEN EXISTS (SELECT 1 FROM dbo.CMS_ContentImages i WHERE i.ContentId = c.Id) THEN 0 ELSE 1 END,
                 CASE WHEN EXISTS (SELECT 1 FROM dbo.CMS_ContentMetadata m WHERE m.ContentId = c.Id) THEN 0 ELSE 1 END,
                 c.Id;
        IF @Src IS NULL
            THROW 50406, N'WebsiteApplicationId owns no content to clone.', 1;

        INSERT INTO #Override VALUES (N'ApplicationId', CAST(@B AS nvarchar(12)));
        EXEC #CloneRow N'CMS_Contents', @Src, @ContentB OUTPUT;

        SET @Tmp = (SELECT TOP (1) Id FROM dbo.CMS_ContentMetadata WHERE ContentId = @Src ORDER BY Id);
        IF @Tmp IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ContentId', CAST(@ContentB AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentMetadata', @Tmp, @N OUTPUT;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'CMS_ContentMetadata', IIF(@Tmp IS NULL, N'no source row', N'cloned'), IIF(@Tmp IS NULL, N'REVIEW', N'INFO'));

        SET @Tmp = (SELECT TOP (1) Id FROM dbo.CMS_ContentImages WHERE ContentId = @Src ORDER BY Id);
        IF @Tmp IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ContentId', CAST(@ContentB AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentImages', @Tmp, @N OUTPUT;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'CMS_ContentImages', IIF(@Tmp IS NULL, N'no source row', N'cloned'), IIF(@Tmp IS NULL, N'REVIEW', N'INFO'));

        SET @Tmp = (SELECT TOP (1) Id FROM dbo.CMS_ContentTranslations WHERE ContentId = @Src ORDER BY Id);
        IF @Tmp IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ContentId', CAST(@ContentB AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentTranslations', @Tmp, @N OUTPUT;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'CMS_ContentTranslations', IIF(@Tmp IS NULL, N'no source row', N'cloned'), IIF(@Tmp IS NULL, N'REVIEW', N'INFO'));

        SELECT TOP (1) @SectionSrc = s.Id FROM dbo.CMS_ContentSections s WHERE s.ContentId = @Src
        ORDER BY CASE WHEN EXISTS (SELECT 1 FROM dbo.CMS_SectionElements e WHERE e.SectionId = s.Id) THEN 0 ELSE 1 END, s.Id;
        IF @SectionSrc IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ContentId', CAST(@ContentB AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentSections', @SectionSrc, @SectionB OUTPUT;
            SET @Tmp = (SELECT TOP (1) Id FROM dbo.CMS_SectionElements WHERE SectionId = @SectionSrc ORDER BY Id);
            IF @Tmp IS NOT NULL
            BEGIN
                INSERT INTO #Override VALUES (N'SectionId', CAST(@SectionB AS nvarchar(12)));
                EXEC #CloneRow N'CMS_SectionElements', @Tmp, @N OUTPUT;
            END;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'CMS_ContentSections', IIF(@SectionSrc IS NULL, N'no source row', N'cloned'), IIF(@SectionSrc IS NULL, N'REVIEW', N'INFO'));
        INSERT INTO @Checks VALUES (N'seed', N'CMS_SectionElements', IIF(@SectionB IS NULL OR @Tmp IS NULL, N'no source row', N'cloned'), IIF(@SectionB IS NULL OR @Tmp IS NULL, N'REVIEW', N'INFO'));

        SET @CatA = (SELECT TOP (1) Id FROM dbo.CMS_Categories WHERE ApplicationId = @A ORDER BY Id);
        IF @CatA IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ApplicationId', CAST(@B AS nvarchar(12))), (N'ParentId', N'0');
            EXEC #CloneRow N'CMS_Categories', @CatA, @CatB OUTPUT;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'CMS_Categories', IIF(@CatA IS NULL, N'no source row', N'cloned'), IIF(@CatA IS NULL, N'REVIEW', N'INFO'));

        SET @TagA = (SELECT TOP (1) Id FROM dbo.GNR_Tags WHERE ApplicationId = @A ORDER BY Id);
        IF @TagA IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ApplicationId', CAST(@B AS nvarchar(12)));
            EXEC #CloneRow N'GNR_Tags', @TagA, @TagB OUTPUT;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'GNR_Tags', IIF(@TagA IS NULL, N'no source row', N'cloned'), IIF(@TagA IS NULL, N'REVIEW', N'INFO'));

        -- Relations: B content -> B term (B-owned), A content -> B term and B content -> A term
        -- (cross-tenant; invisible to both mapped principals).
        SET @Tmp = (SELECT TOP (1) Id FROM dbo.CMS_ContentInCategories ORDER BY Id);
        IF @Tmp IS NOT NULL AND @CatB IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ContentId', CAST(@ContentB AS nvarchar(12))), (N'CategoryId', CAST(@CatB AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentInCategories', @Tmp, @N OUTPUT;
            INSERT INTO #Override VALUES (N'ContentId', CAST(@Src AS nvarchar(12))), (N'CategoryId', CAST(@CatB AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentInCategories', @Tmp, @N OUTPUT;
            INSERT INTO #Override VALUES (N'ContentId', CAST(@ContentB AS nvarchar(12))), (N'CategoryId', CAST(@CatA AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentInCategories', @Tmp, @N OUTPUT;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'CMS_ContentInCategories', IIF(@Tmp IS NULL OR @CatB IS NULL, N'no source row', N'cloned (own + 2 cross-tenant)'), IIF(@Tmp IS NULL OR @CatB IS NULL, N'REVIEW', N'INFO'));

        SET @Tmp = (SELECT TOP (1) Id FROM dbo.CMS_ContentInTags ORDER BY Id);
        IF @Tmp IS NOT NULL AND @TagB IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ContentId', CAST(@ContentB AS nvarchar(12))), (N'TagId', CAST(@TagB AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentInTags', @Tmp, @N OUTPUT;
            INSERT INTO #Override VALUES (N'ContentId', CAST(@Src AS nvarchar(12))), (N'TagId', CAST(@TagB AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentInTags', @Tmp, @N OUTPUT;
            INSERT INTO #Override VALUES (N'ContentId', CAST(@ContentB AS nvarchar(12))), (N'TagId', CAST(@TagA AS nvarchar(12)));
            EXEC #CloneRow N'CMS_ContentInTags', @Tmp, @N OUTPUT;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'CMS_ContentInTags', IIF(@Tmp IS NULL OR @TagB IS NULL, N'no source row', N'cloned (own + 2 cross-tenant)'), IIF(@Tmp IS NULL OR @TagB IS NULL, N'REVIEW', N'INFO'));

        -- Tenant-owned cultures for both applications (must be invisible to both).
        SET @Tmp = (SELECT TOP (1) Id FROM dbo.GNR_Cultures WHERE ApplicationId = 0 ORDER BY Id);
        IF @Tmp IS NOT NULL
        BEGIN
            INSERT INTO #Override VALUES (N'ApplicationId', CAST(@B AS nvarchar(12))), (N'Key', N'N''zz-cdb''');
            EXEC #CloneRow N'GNR_Cultures', @Tmp, @N OUTPUT;
            INSERT INTO #Override VALUES (N'ApplicationId', CAST(@A AS nvarchar(12))), (N'Key', N'N''zz-cda''');
            EXEC #CloneRow N'GNR_Cultures', @Tmp, @N OUTPUT;
        END;
        INSERT INTO @Checks VALUES (N'seed', N'GNR_Cultures (tenant-owned A and B)', IIF(@Tmp IS NULL, N'no global culture', N'cloned'), IIF(@Tmp IS NULL, N'REVIEW', N'INFO'));
    END;

    -------------------------------------------------------------------------------------------
    -- Principals under test: the real website login, a probe mapped to B, and an unmapped role
    -- member. Probe users and their mapping row are rolled back with everything else.
    -------------------------------------------------------------------------------------------
    CREATE USER cd_rls_probe_other WITHOUT LOGIN;
    CREATE USER cd_rls_probe_unmapped WITHOUT LOGIN;
    ALTER ROLE ContentDeliveryWebsiteReader ADD MEMBER cd_rls_probe_other;
    ALTER ROLE ContentDeliveryWebsiteReader ADD MEMBER cd_rls_probe_unmapped;
    INSERT INTO ContentDeliverySecurity.WebsitePrincipalApplication (PrincipalName, PrincipalSid, ApplicationId)
    SELECT name, sid, @B FROM sys.database_principals WHERE name = N'cd_rls_probe_other';

    INSERT INTO @Principals VALUES (N'website', 'LOGIN', @Login, @A),
                                   (N'probe mapped to B', 'USER', N'cd_rls_probe_other', @B),
                                   (N'probe unmapped role member', 'USER', N'cd_rls_probe_unmapped', NULL);

    -------------------------------------------------------------------------------------------
    -- Visible rows per principal (plain SQL as that principal; no SDK involved).
    -------------------------------------------------------------------------------------------
    DECLARE principals CURSOR LOCAL FAST_FORWARD FOR SELECT Label, Kind, Name FROM @Principals;
    OPEN principals;
    FETCH NEXT FROM principals INTO @Label, @Kind, @Name;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @Kind = 'LOGIN' EXECUTE AS LOGIN = @Name; ELSE EXECUTE AS USER = @Name;

        INSERT INTO @Visible SELECT @Label, N'CMS_Contents', Id FROM dbo.CMS_Contents;
        INSERT INTO @Visible SELECT @Label, N'CMS_ContentMetadata', Id FROM dbo.CMS_ContentMetadata;
        INSERT INTO @Visible SELECT @Label, N'CMS_ContentSections', Id FROM dbo.CMS_ContentSections;
        INSERT INTO @Visible SELECT @Label, N'CMS_SectionElements', Id FROM dbo.CMS_SectionElements;
        INSERT INTO @Visible SELECT @Label, N'CMS_ContentImages', Id FROM dbo.CMS_ContentImages;
        INSERT INTO @Visible SELECT @Label, N'CMS_Categories', Id FROM dbo.CMS_Categories;
        INSERT INTO @Visible SELECT @Label, N'GNR_Tags', Id FROM dbo.GNR_Tags;
        INSERT INTO @Visible SELECT @Label, N'CMS_ContentInCategories', Id FROM dbo.CMS_ContentInCategories;
        INSERT INTO @Visible SELECT @Label, N'CMS_ContentInTags', Id FROM dbo.CMS_ContentInTags;
        INSERT INTO @Visible SELECT @Label, N'GNR_Cultures', Id FROM dbo.GNR_Cultures;
        INSERT INTO @Visible SELECT @Label, N'CMS_ContentTranslations', Id FROM dbo.CMS_ContentTranslations;

        REVERT;
        FETCH NEXT FROM principals INTO @Label, @Kind, @Name;
    END;
    CLOSE principals;
    DEALLOCATE principals;

    -------------------------------------------------------------------------------------------
    -- Ground truth, computed by this unrestricted DBA session.
    -------------------------------------------------------------------------------------------
    INSERT INTO @Allowed SELECT p.Label, N'CMS_Contents', c.Id
        FROM @Principals p JOIN dbo.CMS_Contents c ON c.ApplicationId = p.ApplicationId;
    INSERT INTO @Allowed SELECT p.Label, N'CMS_ContentMetadata', x.Id
        FROM @Principals p JOIN dbo.CMS_Contents c ON c.ApplicationId = p.ApplicationId JOIN dbo.CMS_ContentMetadata x ON x.ContentId = c.Id;
    INSERT INTO @Allowed SELECT p.Label, N'CMS_ContentSections', x.Id
        FROM @Principals p JOIN dbo.CMS_Contents c ON c.ApplicationId = p.ApplicationId JOIN dbo.CMS_ContentSections x ON x.ContentId = c.Id;
    INSERT INTO @Allowed SELECT p.Label, N'CMS_SectionElements', e.Id
        FROM @Principals p JOIN dbo.CMS_Contents c ON c.ApplicationId = p.ApplicationId
        JOIN dbo.CMS_ContentSections s ON s.ContentId = c.Id JOIN dbo.CMS_SectionElements e ON e.SectionId = s.Id;
    INSERT INTO @Allowed SELECT p.Label, N'CMS_ContentImages', x.Id
        FROM @Principals p JOIN dbo.CMS_Contents c ON c.ApplicationId = p.ApplicationId JOIN dbo.CMS_ContentImages x ON x.ContentId = c.Id;
    INSERT INTO @Allowed SELECT p.Label, N'CMS_Categories', t.Id
        FROM @Principals p JOIN dbo.CMS_Categories t ON t.ApplicationId = p.ApplicationId;
    INSERT INTO @Allowed SELECT p.Label, N'GNR_Tags', t.Id
        FROM @Principals p JOIN dbo.GNR_Tags t ON t.ApplicationId = p.ApplicationId;
    INSERT INTO @Allowed SELECT p.Label, N'CMS_ContentInCategories', r.Id
        FROM @Principals p JOIN dbo.CMS_ContentInCategories r ON 1 = 1
        JOIN dbo.CMS_Contents c ON c.Id = r.ContentId AND c.ApplicationId = p.ApplicationId
        JOIN dbo.CMS_Categories t ON t.Id = r.CategoryId AND t.ApplicationId = p.ApplicationId;
    INSERT INTO @Allowed SELECT p.Label, N'CMS_ContentInTags', r.Id
        FROM @Principals p JOIN dbo.CMS_ContentInTags r ON 1 = 1
        JOIN dbo.CMS_Contents c ON c.Id = r.ContentId AND c.ApplicationId = p.ApplicationId
        JOIN dbo.GNR_Tags t ON t.Id = r.TagId AND t.ApplicationId = p.ApplicationId;
    INSERT INTO @Allowed SELECT p.Label, N'GNR_Cultures', k.Id
        FROM @Principals p JOIN dbo.GNR_Cultures k ON k.ApplicationId = 0 WHERE p.ApplicationId IS NOT NULL;
    INSERT INTO @Allowed SELECT p.Label, N'CMS_ContentTranslations', x.Id
        FROM @Principals p JOIN dbo.CMS_Contents c ON c.ApplicationId = p.ApplicationId JOIN dbo.CMS_ContentTranslations x ON x.ContentId = c.Id;

    INSERT INTO @Totals
    SELECT N'CMS_Contents', COUNT(*) FROM dbo.CMS_Contents UNION ALL
    SELECT N'CMS_ContentMetadata', COUNT(*) FROM dbo.CMS_ContentMetadata UNION ALL
    SELECT N'CMS_ContentSections', COUNT(*) FROM dbo.CMS_ContentSections UNION ALL
    SELECT N'CMS_SectionElements', COUNT(*) FROM dbo.CMS_SectionElements UNION ALL
    SELECT N'CMS_ContentImages', COUNT(*) FROM dbo.CMS_ContentImages UNION ALL
    SELECT N'CMS_Categories', COUNT(*) FROM dbo.CMS_Categories UNION ALL
    SELECT N'GNR_Tags', COUNT(*) FROM dbo.GNR_Tags UNION ALL
    SELECT N'CMS_ContentInCategories', COUNT(*) FROM dbo.CMS_ContentInCategories UNION ALL
    SELECT N'CMS_ContentInTags', COUNT(*) FROM dbo.CMS_ContentInTags UNION ALL
    SELECT N'GNR_Cultures', COUNT(*) FROM dbo.GNR_Cultures UNION ALL
    SELECT N'CMS_ContentTranslations', COUNT(*) FROM dbo.CMS_ContentTranslations;

    INSERT INTO @Results (Label, TableName, AllowedRows, VisibleRows, LeakedRows, MissingRows, ForeignRows)
    SELECT p.Label, t.TableName,
           (SELECT COUNT(*) FROM @Allowed a WHERE a.Label = p.Label AND a.TableName = t.TableName),
           (SELECT COUNT(*) FROM @Visible v WHERE v.Label = p.Label AND v.TableName = t.TableName),
           (SELECT COUNT(*) FROM @Visible v WHERE v.Label = p.Label AND v.TableName = t.TableName
                AND NOT EXISTS (SELECT 1 FROM @Allowed a WHERE a.Label = v.Label AND a.TableName = v.TableName AND a.RowId = v.RowId)),
           (SELECT COUNT(*) FROM @Allowed a WHERE a.Label = p.Label AND a.TableName = t.TableName
                AND NOT EXISTS (SELECT 1 FROM @Visible v WHERE v.Label = a.Label AND v.TableName = a.TableName AND v.RowId = a.RowId)),
           t.TotalRows - (SELECT COUNT(*) FROM @Allowed a WHERE a.Label = p.Label AND a.TableName = t.TableName)
    FROM @Principals p CROSS JOIN @Totals t;

    -- PASS needs rows that should be hidden; otherwise the table proves nothing.
    UPDATE @Results SET Verdict = CASE WHEN LeakedRows > 0 OR MissingRows > 0 THEN N'FAIL'
                                       WHEN ForeignRows = 0 THEN N'INCONCLUSIVE'
                                       ELSE N'PASS' END;

    INSERT INTO @Checks (Label, CheckName, Observed, Verdict)
    SELECT p.Label, N'active global cultures readable',
           CONCAT(COUNT(k.Id), N' visible'),
           CASE WHEN p.ApplicationId IS NULL THEN IIF(COUNT(k.Id) = 0, N'PASS', N'FAIL')
                ELSE IIF(COUNT(k.Id) > 0, N'PASS', N'FAIL') END
    FROM @Principals p
    LEFT JOIN @Visible v ON v.Label = p.Label AND v.TableName = N'GNR_Cultures'
    LEFT JOIN dbo.GNR_Cultures k ON k.Id = v.RowId AND k.ApplicationId = 0 AND k.IsActive = 1 AND k.IsDeleted = 0
    GROUP BY p.Label, p.ApplicationId;

    -------------------------------------------------------------------------------------------
    -- Spoofing: caller-controlled session state must not change what the website sees.
    -------------------------------------------------------------------------------------------
    EXECUTE AS LOGIN = @Login;
    EXEC sys.sp_set_session_context @key = N'ApplicationId', @value = @B;
    EXEC sys.sp_set_session_context @key = N'TenantId', @value = @B;
    SET @Ci = CAST(@B AS varbinary(128));
    SET CONTEXT_INFO @Ci;
    SELECT @N = (SELECT COUNT(*) FROM dbo.CMS_Contents WHERE ApplicationId <> @A)
              + (SELECT COUNT(*) FROM dbo.CMS_Categories WHERE ApplicationId <> @A)
              + (SELECT COUNT(*) FROM dbo.GNR_Tags WHERE ApplicationId <> @A)
              + (SELECT COUNT(*) FROM dbo.GNR_Cultures WHERE ApplicationId <> 0);
    SET @Tmp = (SELECT COUNT(*) FROM dbo.CMS_Contents);
    EXEC sys.sp_set_session_context @key = N'ApplicationId', @value = NULL;
    EXEC sys.sp_set_session_context @key = N'TenantId', @value = NULL;
    SET CONTEXT_INFO 0x;
    REVERT;
    INSERT INTO @Checks VALUES (N'website', N'spoof: SESSION_CONTEXT and CONTEXT_INFO set to application B',
        CONCAT(N'foreign rows visible=', @N, N'; contents visible=', @Tmp, N' (baseline ',
               (SELECT VisibleRows FROM @Results WHERE Label = N'website' AND TableName = N'CMS_Contents'), N')'),
        IIF(@N = 0 AND @Tmp = (SELECT VisibleRows FROM @Results WHERE Label = N'website' AND TableName = N'CMS_Contents'), N'PASS', N'FAIL'));

    -------------------------------------------------------------------------------------------
    -- Permission probes. Denied probes must fail with a permission error; allowed probes are the
    -- query shapes the SDK issues (COUNT, EXISTS, filtered TOP) and must succeed.
    -------------------------------------------------------------------------------------------
    INSERT INTO @Probes (CheckName, ProbeSql, ExpectDenied) VALUES
        (N'write: UPDATE CMS_Contents', N'UPDATE dbo.CMS_Contents SET IsActive = IsActive WHERE 1 = 0;', 1),
        (N'write: INSERT CMS_ContentInTags', N'INSERT INTO dbo.CMS_ContentInTags (ContentId, TagId) SELECT ContentId, TagId FROM dbo.CMS_ContentInTags WHERE 1 = 0;', 1),
        (N'write: DELETE CMS_ContentTranslations', N'DELETE FROM dbo.CMS_ContentTranslations WHERE 1 = 0;', 1),
        (N'write: UPDATE GNR_Cultures', N'UPDATE dbo.GNR_Cultures SET IsActive = IsActive WHERE 1 = 0;', 1),
        (N'ddl: CREATE TABLE', N'CREATE TABLE dbo.cd_rls_probe_table (Id int);', 1),
        (N'mapping table: SELECT', N'DECLARE @n int; SELECT @n = COUNT(*) FROM ContentDeliverySecurity.WebsitePrincipalApplication;', 1),
        (N'mapping table: INSERT', N'INSERT INTO ContentDeliverySecurity.WebsitePrincipalApplication (PrincipalName, PrincipalSid, ApplicationId) SELECT USER_NAME(), 0x00, 1 WHERE 1 = 0;', 1),
        (N'predicate function: SELECT', N'DECLARE @n int; SELECT @n = COUNT(*) FROM ContentDeliverySecurity.fn_CallerScope();', 1),
        (N'policy: disable', N'ALTER SECURITY POLICY ContentDeliverySecurity.WebsiteTenantPolicy WITH (STATE = OFF);', 1),
        (N'role: join db_owner', N'DECLARE @s nvarchar(300) = N''ALTER ROLE db_owner ADD MEMBER '' + QUOTENAME(USER_NAME()) + N'';''; EXEC (@s);', 1),
        (N'impersonate dbo', N'EXECUTE AS USER = ''dbo''; REVERT;', 1),
        (N'unrelated table: GNR_Applications', N'DECLARE @n int; SELECT @n = COUNT(*) FROM dbo.GNR_Applications;', 1),
        (N'unmapped column: CMS_Contents.CreatedDT', N'DECLARE @d datetime2; SELECT @d = MAX(CreatedDT) FROM dbo.CMS_Contents;', 1),
        (N'unmapped column: CMS_ContentTranslations.Provider', N'DECLARE @s nvarchar(400); SELECT @s = MAX(Provider) FROM dbo.CMS_ContentTranslations;', 1),
        (N'sdk shape: COUNT(*)', N'DECLARE @n int; SELECT @n = COUNT(*) FROM dbo.CMS_Contents WHERE IsActive = 1 AND IsDeleted = 0;', 0),
        (N'sdk shape: EXISTS on relation', N'DECLARE @n int = 0; IF EXISTS (SELECT 1 FROM dbo.CMS_ContentInCategories) SET @n = 1;', 0),
        (N'sdk shape: filtered TOP on cultures', N'DECLARE @n int; SELECT TOP (1) @n = Id FROM dbo.GNR_Cultures WHERE ApplicationId = 0 AND IsActive = 1 AND IsDeleted = 0 ORDER BY Id;', 0);

    DECLARE principals CURSOR LOCAL FAST_FORWARD FOR SELECT Label, Kind, Name FROM @Principals;
    OPEN principals;
    FETCH NEXT FROM principals INTO @Label, @Kind, @Name;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @Kind = 'LOGIN' EXECUTE AS LOGIN = @Name; ELSE EXECUTE AS USER = @Name;

        -- Every other object: no SELECT/EXECUTE may be effective.
        SELECT @N = COUNT(*) FROM sys.objects o
        WHERE o.is_ms_shipped = 0 AND o.type IN ('U', 'V', 'IF', 'TF', 'P', 'FN')
          AND o.object_id NOT IN (OBJECT_ID(N'dbo.CMS_Contents'), OBJECT_ID(N'dbo.CMS_ContentMetadata'),
              OBJECT_ID(N'dbo.CMS_ContentSections'), OBJECT_ID(N'dbo.CMS_SectionElements'), OBJECT_ID(N'dbo.CMS_ContentImages'),
              OBJECT_ID(N'dbo.CMS_Categories'), OBJECT_ID(N'dbo.GNR_Tags'), OBJECT_ID(N'dbo.CMS_ContentInCategories'),
              OBJECT_ID(N'dbo.CMS_ContentInTags'), OBJECT_ID(N'dbo.GNR_Cultures'), OBJECT_ID(N'dbo.CMS_ContentTranslations'))
          AND (HAS_PERMS_BY_NAME(QUOTENAME(SCHEMA_NAME(o.schema_id)) + N'.' + QUOTENAME(o.name), N'OBJECT', N'SELECT') = 1
               OR HAS_PERMS_BY_NAME(QUOTENAME(SCHEMA_NAME(o.schema_id)) + N'.' + QUOTENAME(o.name), N'OBJECT', N'EXECUTE') = 1);
        INSERT INTO @Checks VALUES (@Label, N'unrelated objects with effective SELECT/EXECUTE', CONCAT(@N, N' objects'), IIF(@N = 0, N'PASS', N'FAIL'));

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
            -- 229/230 object/column permission, 262/297/300/15247 statement permission,
            -- 15151 object not found or no permission, 15517 cannot impersonate.
            INSERT INTO @Checks VALUES (@Label, @CheckName, IIF(@Err = 0, N'succeeded', CONCAT(N'error ', @Err)),
                CASE WHEN @ExpectDenied = 0 THEN IIF(@Err = 0, N'PASS', N'FAIL')
                     WHEN @Err = 0 THEN N'FAIL'
                     WHEN @Err IN (229, 230, 262, 297, 300, 15151, 15247, 15517) THEN N'PASS'
                     ELSE N'REVIEW' END);
            FETCH NEXT FROM probes INTO @CheckName, @ProbeSql, @ExpectDenied;
        END;
        CLOSE probes;
        DEALLOCATE probes;

        REVERT;
        FETCH NEXT FROM principals INTO @Label, @Kind, @Name;
    END;
    CLOSE principals;
    DEALLOCATE principals;
END TRY
BEGIN CATCH
    IF ORIGINAL_LOGIN() <> SUSER_SNAME() REVERT;
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Always roll back: seeded rows, probe users and the probe mapping must not survive.
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

INSERT INTO @Checks VALUES (N'cleanup', N'rolled back: probe users and seeded application gone',
    CONCAT(N'probe users=', (SELECT COUNT(*) FROM sys.database_principals WHERE name LIKE N'cd[_]rls[_]probe[_]%'),
           N'; seeded application=', IIF(@ClonedApplication = 1, (SELECT COUNT(*) FROM dbo.GNR_Applications WHERE Id = @B), 0)),
    IIF(NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name LIKE N'cd[_]rls[_]probe[_]%')
        AND (@ClonedApplication = 0 OR NOT EXISTS (SELECT 1 FROM dbo.GNR_Applications WHERE Id = @B)), N'PASS', N'FAIL'));

-- Evidence (counts and verdicts only). Attach all three result sets to the change ticket.
SELECT Label, TableName, AllowedRows, VisibleRows, LeakedRows, MissingRows, ForeignRows, Verdict
FROM @Results ORDER BY Label, TableName;

SELECT Label, CheckName, Observed, Verdict FROM @Checks ORDER BY Seq;

SELECT CASE WHEN EXISTS (SELECT 1 FROM @Results WHERE Verdict = N'FAIL') OR EXISTS (SELECT 1 FROM @Checks WHERE Verdict = N'FAIL') THEN N'FAIL'
            WHEN EXISTS (SELECT 1 FROM @Results WHERE Verdict = N'INCONCLUSIVE') OR EXISTS (SELECT 1 FROM @Checks WHERE Verdict = N'REVIEW') THEN N'INCONCLUSIVE'
            ELSE N'PASS' END AS OverallVerdict,
       @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName, SYSUTCDATETIME() AS VerifiedAtUtc, ORIGINAL_LOGIN() AS VerifiedBy;
