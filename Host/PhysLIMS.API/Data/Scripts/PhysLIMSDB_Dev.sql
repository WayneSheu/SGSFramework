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

USE [PhysLIMSDB_Dev];
GO

-- 1. 確保 deploy_sgs_user 具備資料庫使用者身份 (若尚未映射)
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'deploy_sgs_user')
BEGIN
    CREATE USER [deploy_sgs_user] FOR LOGIN [deploy_sgs_user];
END
GO

-- 2. 授予 VIEW LEDGER CONTENT 權限
GRANT VIEW LEDGER CONTENT TO [deploy_sgs_user];
GO


USE [master];
GO
-- 2. 啟用該資料庫的 ALLOW_SNAPSHOT_ISOLATION
ALTER DATABASE [PhysLIMSDB_Dev]
SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

-- 3. (可選) 建議同步檢查並啟用 READ_COMMITTED_SNAPSHOT 以提升高並發效能
ALTER DATABASE [PhysLIMSDB_Dev] 
SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

