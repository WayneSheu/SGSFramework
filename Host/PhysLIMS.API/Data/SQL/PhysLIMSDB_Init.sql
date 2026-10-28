-- ============================================================================
-- 1. 建立 Server Logins (預設資料庫先設為 master，避免 DB 未建立時報錯)
-- ============================================================================
USE [master];
GO
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'app_sgs_user')
BEGIN
    CREATE LOGIN [app_sgs_user] 
    WITH PASSWORD = N'ZAQ!2wsx', 
         DEFAULT_DATABASE = [master],
         CHECK_EXPIRATION = OFF,
         CHECK_POLICY = ON;
END
GO

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'deploy_sgs_user')
BEGIN
    CREATE LOGIN [deploy_sgs_user]
    WITH PASSWORD = N'ZAQ!2wsx',
         DEFAULT_DATABASE = [master],
         CHECK_EXPIRATION = OFF,
         CHECK_POLICY = ON;
END
GO

-- 資料庫備份專用帳號 (BackupConnection，僅 db_backupoperator；密碼須與 BackupConnection 一致)
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'backup_sgs_user')
BEGIN
    CREATE LOGIN [backup_sgs_user]
    WITH PASSWORD = N'ZAQ!2wsx',
         DEFAULT_DATABASE = [master],
         CHECK_EXPIRATION = OFF,
         CHECK_POLICY = ON;
END
GO

-- ============================================================================
-- 2. 切換至目標資料庫並建立 Schema (若資料庫未建立，請先建立 DB)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'PhysLIMSDB')
BEGIN
    CREATE DATABASE [PhysLIMSDB];
END
GO

USE [PhysLIMSDB];
GO

-- 修改 Login 之預設資料庫
ALTER LOGIN [app_sgs_user] WITH DEFAULT_DATABASE = [PhysLIMSDB];
ALTER LOGIN [deploy_sgs_user] WITH DEFAULT_DATABASE = [PhysLIMSDB];
ALTER LOGIN [backup_sgs_user] WITH DEFAULT_DATABASE = [PhysLIMSDB];
GO

-- 建立多模組獨立 Schemas
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'core') EXEC('CREATE SCHEMA [core];');
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'org') EXEC('CREATE SCHEMA [org];');
-- 排程框架 (Hangfire) 資料表，由 deploy_sgs_user 於 API 啟動時建立
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'hangfire') EXEC('CREATE SCHEMA [hangfire] AUTHORIZATION [dbo];');

GO

-- ============================================================================
-- 3. 建立 Database Users 與設定權限
-- ============================================================================

-- A. 設定 AP 運行期帳號 (DML 權限)
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'app_sgs_user')
BEGIN
    CREATE USER [app_sgs_user] FOR LOGIN [app_sgs_user];
END
GO

GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::[core] TO [app_sgs_user];
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::[org] TO [app_sgs_user];
-- 排程作業佇列、狀態與分散式鎖 (sp_getapplock 屬 public 權限)
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[hangfire] TO [app_sgs_user];

GO

-- B. 設定 CI/CD 部署帳號 (DDL 權限)
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'deploy_sgs_user')
BEGIN
    CREATE USER [deploy_sgs_user] FOR LOGIN [deploy_sgs_user];
END
GO

ALTER ROLE [db_ddladmin] ADD MEMBER [deploy_sgs_user];
ALTER ROLE [db_datareader] ADD MEMBER [deploy_sgs_user];
ALTER ROLE [db_datawriter] ADD MEMBER [deploy_sgs_user];

GRANT ALTER, CONTROL ON SCHEMA::[core] TO [deploy_sgs_user];
GRANT ALTER, CONTROL ON SCHEMA::[org] TO [deploy_sgs_user];
GRANT ALTER, CONTROL ON SCHEMA::[hangfire] TO [deploy_sgs_user];
GO

-- C. 設定資料庫備份帳號 (僅備份權限；備份目錄寫入權限需授予 SQL Server 服務帳號)
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'backup_sgs_user')
BEGIN
    CREATE USER [backup_sgs_user] FOR LOGIN [backup_sgs_user];
END
GO

ALTER ROLE [db_backupoperator] ADD MEMBER [backup_sgs_user];
GO

-- ============================================================================
-- 4. 設定 Ledger 權限
-- ============================================================================
-- 啟用 SNAPSHOT ISOLATION (允許應用程式發起 Snapshot Transaction)
ALTER DATABASE [PhysLIMSDB]
SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

-- 3. (選用) 若希望所有預設的 READ COMMITTED 事務自動改用快照列版本控管，可加開以下設定：
ALTER DATABASE [PhysLIMSDB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
 GO

-- 4. 驗證資料庫設定狀態
SELECT 
    name AS DatabaseName,
    snapshot_isolation_state_desc AS SnapshotIsolationStatus,
    is_read_committed_snapshot_on AS IsReadCommittedSnapshotOn
FROM sys.databases
WHERE name = N'PhysLIMSDB';
GO

-- 針對指定資料庫使用者授予 VIEW LEDGER CONTENT 權限
USE [PhysLIMSDB];
GO

-- 授權給指定角色或使用者  授予 VIEW LEDGER CONTENT 權限
GRANT VIEW LEDGER CONTENT TO [deploy_sgs_user];
GRANT VIEW LEDGER CONTENT TO [app_sgs_user];
GO

-- ============================================================================
-- 5. 驗證帳號 (預期三列皆 is_disabled = 0；backup_sgs_user 的 backup_op = 1)
-- ============================================================================
SELECT login_name = sp.name, sp.is_disabled, db_user = dp.name,
       backup_op = IS_ROLEMEMBER('db_backupoperator', sp.name)
FROM sys.server_principals sp
LEFT JOIN sys.database_principals dp ON dp.sid = sp.sid
WHERE sp.name IN (N'app_sgs_user', N'deploy_sgs_user', N'backup_sgs_user');
GO