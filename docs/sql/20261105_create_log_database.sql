/* =============================================================================
   PhysLIMS 日誌管理外掛模組 (SGS.Modules.LOG) 獨立資料庫建置與權限設定
   執行身分：DBA (sysadmin)    對象：新資料庫 PhysLIMSLogDB（與主庫 PhysLIMSDB 分離）A
   - 系統日誌 / 資安日誌 / 資料日誌改寫入本資料庫的 [log] Schema（SQL Ledger APPEND_ONLY）
   - 資料表由 API 啟動時 LogMigrationService 以部署帳號建立 (LOGDB_Initial)；本腳本只負責建庫、使用者與授權
   - 既有主庫的 core.SystemLogs / core.SecurityLog / core.AuditLogs（含各模組 *.AuditLogs）為帳本表，無法刪除，
     保留於主庫作為唯讀封存，不搬移（雜湊鏈與 Ledger digest 維持完整）
   - 連線字串（以環境變數注入，勿寫入版控）：
       PersistentSettings__ConnectionStrings__LogConnection           → 執行期帳號（僅 SELECT / INSERT）
       PersistentSettings__ConnectionStrings__LogMigrationConnection  → 部署帳號（DDL）
   - 兩個登入帳號需事先存在；預設沿用主庫的 app_sgs_user / deploy_sgs_user（可於下方變數改為專用帳號）
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
GO

-- 6. Ledger 驗證 (sp_verify_database_ledger) 需啟用快照隔離
IF (SELECT snapshot_isolation_state FROM sys.databases WHERE name = N'PhysLIMSLogDB') = 0
    ALTER DATABASE [PhysLIMSLogDB] SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

/* 驗證：預期 log schema 存在，兩個使用者皆有對應權限 */
SELECT dp.name AS principal, p.permission_name, p.state_desc, s.name AS on_schema
FROM sys.database_permissions p
JOIN sys.database_principals dp ON dp.principal_id = p.grantee_principal_id
LEFT JOIN sys.schemas s ON p.class = 3 AND p.major_id = s.schema_id
WHERE dp.name IN (N'app_sgs_user', N'deploy_sgs_user')
ORDER BY dp.name, p.permission_name;
GO
