/* =============================================================================
   PhysLIMS 日誌冷庫：補授權執行期帳號 app_sgs_user 的唯讀存取
   執行身分：DBA (sysadmin)    對象：冷庫 PhysLIMSLogDB_<年度 / 年度H1 / 年度H2>

   為什麼需要：
     審計日誌寫入時（AuditLogStore.ReadLastHashAsync），若熱庫該 Schema 尚無資料（新庫、剛換庫），
     會改從最新冷庫讀取最後一筆 StoredHash，使雜湊鏈跨庫連續。冷庫連線沿用 LogConnection（app_sgs_user），
     但年度換庫是「備份熱庫 → 還原為冷庫」，不會替執行期帳號建立冷庫使用者，
     因此症狀為：「無法開啟登入所要求的資料庫 "PhysLIMSLogDB_2026"。使用者 'app_sgs_user' 的登入失敗」，
     該 Schema 的審計批次落入 Audit_Failures 降級檔，且一直復原失敗。

   授予的權限（最小必要）：
     - 冷庫中建立使用者 app_sgs_user（對應同名登入）
     - 僅授予 log Schema 的 SELECT（冷庫為唯讀庫，不授予 INSERT / UPDATE / DELETE）

   冷庫為 READ_ONLY，無法直接建立使用者，故流程為：
     暫時切換 READ_WRITE（WITH ROLLBACK IMMEDIATE，會中斷該冷庫上的連線）→ 授權 → 還原 READ_ONLY。
     任何一步失敗都會嘗試把冷庫還原為 READ_ONLY，避免冷庫停留在可寫狀態。
     已具備權限的冷庫會直接略過，不切換唯讀狀態。

   參數：
     @hotDb    熱庫名稱（冷庫以「@hotDb + _20xx」比對；須與 LogConnection 的 Database 一致）
     @appLogin 執行期登入帳號（須與 LogConnection 的 User Id 一致）
     @onlyCold 指定單一冷庫名稱；NULL = 處理伺服器上所有符合命名的冷庫

   前置：伺服器需已有登入 app_sgs_user。可重複執行。
   ============================================================================= */
USE [master];
GO

SET NOCOUNT ON;

DECLARE @hotDb    sysname = N'PhysLIMSLogDB';   -- 現行熱庫名稱
DECLARE @appLogin sysname = N'app_sgs_user';    -- 執行期登入帳號
DECLARE @onlyCold sysname = NULL;               -- 例：N'PhysLIMSLogDB_2026'；NULL = 全部冷庫

IF SUSER_ID(@appLogin) IS NULL
BEGIN
    RAISERROR(N'伺服器尚無登入 %s，請先建立後重新執行。', 16, 1, @appLogin);
    RETURN;
END;

DECLARE @cold sysname, @sql nvarchar(max), @isReadOnly bit, @hasAccess bit, @flipped bit;
DECLARE @found int = 0;

DECLARE cold_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT name
    FROM sys.databases
    WHERE name <> @hotDb
      AND name LIKE @hotDb + N'\_20%' ESCAPE '\'
      AND (@onlyCold IS NULL OR name = @onlyCold)
      AND state = 0                                -- ONLINE（還原中的冷庫略過）
    ORDER BY name;

OPEN cold_cursor;
FETCH NEXT FROM cold_cursor INTO @cold;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @found += 1;
    SET @flipped = 0;
    SET @isReadOnly = CASE WHEN DATABASEPROPERTYEX(@cold, 'Updateability') = N'READ_ONLY' THEN 1 ELSE 0 END;

    -- 是否已具備：存在對應該登入的使用者，且 log Schema 有 SELECT
    SET @hasAccess = 0;
    SET @sql = N'USE ' + QUOTENAME(@cold) + N';
        IF EXISTS (SELECT 1 FROM sys.database_principals dp
                   JOIN sys.server_principals sp ON sp.sid = dp.sid
                   WHERE sp.name = @login AND dp.type IN (''S'', ''U'', ''G''))
        BEGIN
            -- 以模擬該使用者驗證實際權限
            DECLARE @u sysname = (SELECT TOP 1 dp.name FROM sys.database_principals dp
                                  JOIN sys.server_principals sp ON sp.sid = dp.sid WHERE sp.name = @login);
            DECLARE @ok int;
            EXECUTE AS USER = @u;
            SET @ok = ISNULL(HAS_PERMS_BY_NAME(N''log'', N''SCHEMA'', N''SELECT''), 0);
            REVERT;
            SET @has = CASE WHEN @ok = 1 THEN 1 ELSE 0 END;
        END;';
    EXEC sp_executesql @sql, N'@login sysname, @has bit OUTPUT', @login = @appLogin, @has = @hasAccess OUTPUT;

    IF @hasAccess = 1
    BEGIN
        PRINT N'[略過] ' + @cold + N'：' + @appLogin + N' 已具備 log Schema SELECT。';
    END
    ELSE
    BEGIN
        BEGIN TRY
            IF @isReadOnly = 1
            BEGIN
                PRINT N'[處理] ' + @cold + N'：暫時切換為 READ_WRITE（會中斷該庫現有連線）...';
                SET @sql = N'ALTER DATABASE ' + QUOTENAME(@cold) + N' SET READ_WRITE WITH ROLLBACK IMMEDIATE;';
                EXEC (@sql);
                SET @flipped = 1;
            END
            ELSE
                PRINT N'[處理] ' + @cold + N'：目前為可寫狀態（非 READ_ONLY），直接授權。';

            SET @sql = N'USE ' + QUOTENAME(@cold) + N';
                DECLARE @existing sysname = (SELECT TOP 1 dp.name FROM sys.database_principals dp
                                             JOIN sys.server_principals sp ON sp.sid = dp.sid WHERE sp.name = @login);
                DECLARE @c nvarchar(600);

                IF @existing IS NULL
                BEGIN
                    -- 若有同名但 SID 不符（孤立使用者，還原自備份常見）則改綁登入；否則新建
                    IF DATABASE_PRINCIPAL_ID(@login) IS NOT NULL
                        SET @c = N''ALTER USER '' + QUOTENAME(@login) + N'' WITH LOGIN = '' + QUOTENAME(@login) + N'';'';
                    ELSE
                        SET @c = N''CREATE USER '' + QUOTENAME(@login) + N'' FOR LOGIN '' + QUOTENAME(@login) + N'';'';
                    EXEC (@c);
                    SET @existing = @login;
                END;

                SET @c = N''GRANT SELECT ON SCHEMA::[log] TO '' + QUOTENAME(@existing) + N'';'';
                EXEC (@c);';
            EXEC sp_executesql @sql, N'@login sysname', @login = @appLogin;

            PRINT N'[完成] ' + @cold + N'：已授予 ' + @appLogin + N' 對 log Schema 的 SELECT。';
        END TRY
        BEGIN CATCH
            PRINT N'[失敗] ' + @cold + N'：' + ERROR_MESSAGE();
        END CATCH;

        -- 無論成功與否都要把冷庫還原為 READ_ONLY
        IF @flipped = 1
        BEGIN
            BEGIN TRY
                SET @sql = N'ALTER DATABASE ' + QUOTENAME(@cold) + N' SET READ_ONLY WITH ROLLBACK IMMEDIATE;';
                EXEC (@sql);
                PRINT N'       ' + @cold + N' 已還原為 READ_ONLY。';
            END TRY
            BEGIN CATCH
                PRINT N'[嚴重] ' + @cold + N' 無法還原為 READ_ONLY，請立即手動執行：ALTER DATABASE '
                    + QUOTENAME(@cold) + N' SET READ_ONLY WITH ROLLBACK IMMEDIATE;  原因：' + ERROR_MESSAGE();
            END CATCH;
        END;
    END;

    FETCH NEXT FROM cold_cursor INTO @cold;
END;

CLOSE cold_cursor;
DEALLOCATE cold_cursor;

IF @found = 0
    PRINT N'提示：找不到符合「' + @hotDb + N'_20xx」且為 ONLINE 的冷庫，未做任何變更。';
GO

/* 驗證：預期每個冷庫的 is_read_only = 1（授權後已還原唯讀）、state_desc = ONLINE */
SELECT d.name AS cold_db, d.is_read_only, d.state_desc
FROM sys.databases d
WHERE d.name LIKE N'PhysLIMSLogDB\_20%' ESCAPE '\'
ORDER BY d.name;
GO

/* 實際以 app_sgs_user 身分確認（把 PhysLIMSLogDB_2026 換成要驗證的冷庫）：
     EXECUTE AS LOGIN = N'app_sgs_user';
     SELECT COUNT(*) FROM [PhysLIMSLogDB_2026].[log].[AuditLogs];
     REVERT;
*/
