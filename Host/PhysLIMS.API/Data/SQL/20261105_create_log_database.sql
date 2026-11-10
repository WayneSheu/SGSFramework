/* =============================================================================
   PhysLIMS 日誌管理外掛模組 (SGS.Modules.LOG) 獨立資料庫建置與權限設定
   執行身分：DBA (sysadmin)    對象：新資料庫 PhysLIMSLogDB（與主庫 PhysLIMSDB 分離）
   - 系統日誌 / 資安日誌 / 資料日誌改寫入本資料庫的 [log] Schema（SQL Ledger APPEND_ONLY）
   - 資料表由 API 啟動時 LogMigrationService 以部署帳號建立 (LOGDB_Initial)；本腳本只負責建庫、使用者與授權
   - 本腳本涵蓋日誌庫需要的全部帳號：執行期、部署、備份（backup_sgs_user → db_backupoperator），可重複執行。
     日誌庫重建（例如開發環境重置）後必須重新執行，否則排程「日誌資料庫完整備份」會因備份帳號無法存取該庫而失敗
   - 若已由 20261105_grant_log_database_autocreate.sql 授權自動建庫，API 首次啟動會自動完成同樣的建庫與授權，
     本腳本改為手動建庫或補授權時使用
   - 既有主庫的 core.SystemLogs / core.SecurityLog / core.AuditLogs（含各模組 *.AuditLogs）為帳本表，無法刪除，
     保留於主庫作為唯讀封存，不搬移（雜湊鏈與 Ledger digest 維持完整）
   - 連線字串（以環境變數注入，勿寫入版控）：
       PersistentSettings__ConnectionStrings__LogConnection           → 執行期帳號（僅 SELECT / INSERT）
       PersistentSettings__ConnectionStrings__LogMigrationConnection  → 部署帳號（DDL）
   - 登入帳號需事先存在；預設沿用主庫的 app_sgs_user / deploy_sgs_user / backup_sgs_user（可於下方變數改為專用帳號）
     backup_sgs_user 由 20261028_grant_scheduler_hangfire_backup.sql 建立；尚未建立登入時本腳本略過備份授權並提示
   ============================================================================= */
USE [master];
GO

IF DB_ID(N'PhysLIMSLogDB') IS NULL
    CREATE DATABASE [PhysLIMSLogDB];
GO

USE [PhysLIMSLogDB];
GO

DECLARE @appLogin    sysname = N'app_sgs_user';     -- 執行期登入帳號
DECLARE @deployLogin sysname = N'deploy_sgs_user';  -- 部署登入帳號
DECLARE @backupLogin sysname = N'backup_sgs_user';  -- 備份登入帳號（BackupConnection；排程備份日誌庫用）
DECLARE @sql nvarchar(max);

-- 1. Database Users
IF DATABASE_PRINCIPAL_ID(@appLogin) IS NULL
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@appLogin) + N' FOR LOGIN ' + QUOTENAME(@appLogin) + N';';
    EXEC (@sql);
END;

IF DATABASE_PRINCIPAL_ID(@deployLogin) IS NULL
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@deployLogin) + N' FOR LOGIN ' + QUOTENAME(@deployLogin) + N';';
    EXEC (@sql);
END;

-- 2. Schema（部署帳號之後需 ALTER / CONTROL 以建立資料表）
IF SCHEMA_ID(N'log') IS NULL
    EXEC (N'CREATE SCHEMA [log] AUTHORIZATION [dbo]');

-- 3. 執行期帳號：日誌只增不改（Ledger APPEND_ONLY，UPDATE / DELETE 本就被引擎拒絕），查詢需 SELECT
SET @sql = N'GRANT SELECT, INSERT ON SCHEMA::[log] TO ' + QUOTENAME(@appLogin) + N';';
EXEC (@sql);

-- 4. 部署帳號：建立 Schema 內物件與 Ledger 資料表
SET @sql = N'GRANT ALTER, CONTROL ON SCHEMA::[log] TO ' + QUOTENAME(@deployLogin) + N';';
EXEC (@sql);
SET @sql = N'GRANT CREATE TABLE TO ' + QUOTENAME(@deployLogin) + N';';
EXEC (@sql);
SET @sql = N'GRANT ENABLE LEDGER TO ' + QUOTENAME(@deployLogin) + N';';
EXEC (@sql);

-- 5. 帳本內容檢視 / 驗證（Ledger 驗證預存程序需要）
SET @sql = N'GRANT VIEW LEDGER CONTENT TO ' + QUOTENAME(@appLogin) + N';';
EXEC (@sql);
SET @sql = N'GRANT VIEW LEDGER CONTENT TO ' + QUOTENAME(@deployLogin) + N';';
EXEC (@sql);

-- 6. 備份帳號：排程作業 database-backup-full-log / database-backup-differential-log 以 BackupConnection 備份本庫，
--    須為本庫使用者並屬於 db_backupoperator（登入尚未建立時略過，請先執行 20261028_grant_scheduler_hangfire_backup.sql）
IF SUSER_ID(@backupLogin) IS NULL
    PRINT N'略過備份帳號授權：伺服器尚無登入 ' + @backupLogin + N'，請先執行 20261028_grant_scheduler_hangfire_backup.sql 後重跑本腳本。'
ELSE
BEGIN
    IF DATABASE_PRINCIPAL_ID(@backupLogin) IS NULL
    BEGIN
        SET @sql = N'CREATE USER ' + QUOTENAME(@backupLogin) + N' FOR LOGIN ' + QUOTENAME(@backupLogin) + N';';
        EXEC (@sql);
    END;

    IF IS_ROLEMEMBER(N'db_backupoperator', @backupLogin) = 0
    BEGIN
        SET @sql = N'ALTER ROLE [db_backupoperator] ADD MEMBER ' + QUOTENAME(@backupLogin) + N';';
        EXEC (@sql);
    END;
END;
GO

-- 7. Ledger 驗證 (sp_verify_database_ledger) 需啟用快照隔離
IF (SELECT snapshot_isolation_state FROM sys.databases WHERE name = N'PhysLIMSLogDB') = 0
    ALTER DATABASE [PhysLIMSLogDB] SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

/* 驗證 1：備份帳號為 db_backupoperator 成員，預期回傳 1（備份登入尚未建立時為 NULL） */
SELECT IS_ROLEMEMBER(N'db_backupoperator', N'backup_sgs_user') AS backup_is_operator;
GO

/* 驗證 2：預期 log schema 存在，執行期 / 部署帳號皆有對應權限 */
SELECT dp.name AS principal, p.permission_name, p.state_desc, s.name AS on_schema
FROM sys.database_permissions p
JOIN sys.database_principals dp ON dp.principal_id = p.grantee_principal_id
LEFT JOIN sys.schemas s ON p.class = 3 AND p.major_id = s.schema_id
WHERE dp.name IN (N'app_sgs_user', N'deploy_sgs_user')
ORDER BY dp.name, p.permission_name;
GO
