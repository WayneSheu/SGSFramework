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

-- ============================================================================
-- 2. 切換至目標資料庫並建立 Schema (若資料庫未建立，請先建立 DB)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'PhysLIMSDB_Dev')
BEGIN
    CREATE DATABASE [PhysLIMSDB_Dev];
END
GO

USE [PhysLIMSDB_Dev];
GO

-- 修改 Login 之預設資料庫
ALTER LOGIN [app_sgs_user] WITH DEFAULT_DATABASE = [PhysLIMSDB_Dev];
ALTER LOGIN [deploy_sgs_user] WITH DEFAULT_DATABASE = [PhysLIMSDB_Dev];
GO

-- 建立多模組獨立 Schemas
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'core') EXEC('CREATE SCHEMA [core];');
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'org') EXEC('CREATE SCHEMA [org];');

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
GO 


-- ============================================================================
-- 4. 設定 Ledger 權限
-- ============================================================================
-- 啟用 SNAPSHOT ISOLATION (允許應用程式發起 Snapshot Transaction)
ALTER DATABASE [PhysLIMSDB_Dev]
SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

-- 3. (選用) 若希望所有預設的 READ COMMITTED 事務自動改用快照列版本控管，可加開以下設定：
ALTER DATABASE [PhysLIMSDB_Dev] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
 GO

-- 4. 驗證資料庫設定狀態
SELECT 
    name AS DatabaseName,
    snapshot_isolation_state_desc AS SnapshotIsolationStatus,
    is_read_committed_snapshot_on AS IsReadCommittedSnapshotOn
FROM sys.databases
WHERE name = N'PhysLIMSDB_Dev';
GO

-- 針對指定資料庫使用者授予 VIEW LEDGER CONTENT 權限
USE [PhysLIMSDB_Dev];
GO

-- 授權給指定角色或使用者  授予 VIEW LEDGER CONTENT 權限
GRANT VIEW LEDGER CONTENT TO [deploy_sgs_user];
GRANT VIEW LEDGER CONTENT TO [app_sgs_user];
GO






