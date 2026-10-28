/* =============================================================================
   PhysLIMS 排程框架 (SGSFramework.Scheduler / Hangfire) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則：
   - Hangfire 資料表由啟動時的部署帳號 (deploy_sgs_user, db_ddladmin) 建立於 [hangfire] Schema
   - 執行期帳號 (app_sgs_user) 僅需 [hangfire] Schema 的 DML
   - 資料庫備份使用專用帳號 (backup_sgs_user)，僅具 db_backupoperator
   ============================================================================= */
USE [PhysLIMSDB];
GO

/* -----------------------------------------------------------------------------
   1. 預先建立 Schema (先於應用程式啟動執行，避免首次啟動時無法授權)
   ----------------------------------------------------------------------------- */
IF SCHEMA_ID(N'hangfire') IS NULL
    EXEC (N'CREATE SCHEMA [hangfire] AUTHORIZATION [dbo]');
GO

/* -----------------------------------------------------------------------------
   2. app_sgs_user：Hangfire 執行期 (作業佇列、狀態、分散式鎖 sp_getapplock 屬 public 權限)
   ----------------------------------------------------------------------------- */
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[hangfire] TO [app_sgs_user];
GO

/* -----------------------------------------------------------------------------
   3. backup_sgs_user：資料庫備份專用帳號 (PersistentSettings:ConnectionStrings:BackupConnection)
      密碼請由 DBA 產生並以環境變數 / claudesettings.json 提供，不得寫入版控
   ----------------------------------------------------------------------------- */
-- USE [master];
-- CREATE LOGIN [backup_sgs_user] WITH PASSWORD = N'<由 DBA 產生的強密碼>', CHECK_POLICY = ON;
-- USE [PhysLIMSDB];
-- 登入帳號不存在時明確報錯（避免靜默略過，應用程式端只會看到 18456 登入失敗）
IF SUSER_ID(N'backup_sgs_user') IS NULL
    RAISERROR(N'找不到登入帳號 backup_sgs_user：請先取消註解上方 CREATE LOGIN 並設定密碼後重新執行本段。', 16, 1);
ELSE IF DATABASE_PRINCIPAL_ID(N'backup_sgs_user') IS NULL
    CREATE USER [backup_sgs_user] FOR LOGIN [backup_sgs_user];
GO
IF DATABASE_PRINCIPAL_ID(N'backup_sgs_user') IS NOT NULL
    ALTER ROLE [db_backupoperator] ADD MEMBER [backup_sgs_user];
GO

-- 備份後驗證 (RESTORE VERIFYONLY) 需要 master 的 CREATE DATABASE 權限，db_backupoperator 不含此權限
-- (未授予時備份檔已產生，但驗證失敗：「於資料庫 'master' 中，CREATE DATABASE 的權限遭拒」)。
-- 僅授予 CREATE DATABASE，不使用 dbcreator (可 ALTER / DROP 任何資料庫)；
-- 若資安政策不允許，請改設 Scheduler:DatabaseBackup:Verify = false (備份仍以 WITH CHECKSUM 檢查頁面)。
USE [master];
GO
IF SUSER_ID(N'backup_sgs_user') IS NOT NULL AND DATABASE_PRINCIPAL_ID(N'backup_sgs_user') IS NULL
    CREATE USER [backup_sgs_user] FOR LOGIN [backup_sgs_user];
IF DATABASE_PRINCIPAL_ID(N'backup_sgs_user') IS NOT NULL
    GRANT CREATE DATABASE TO [backup_sgs_user];
GO
USE [PhysLIMSDB];
GO

-- 驗證備份帳號：登入存在且啟用、資料庫使用者存在且為 db_backupoperator (預期各一列，backup_op = 1)
SELECT login_name = sp.name, sp.is_disabled, db_user = dp.name,
       backup_op = IS_ROLEMEMBER('db_backupoperator', 'backup_sgs_user')
FROM sys.server_principals sp
LEFT JOIN sys.database_principals dp ON dp.sid = sp.sid
WHERE sp.name = N'backup_sgs_user';
GO
-- 備份目錄需授予 SQL Server 服務帳號 (非 backup_sgs_user) 寫入權限；RESTORE VERIFYONLY 不需額外權限

/* -----------------------------------------------------------------------------
   4. 驗證
   ----------------------------------------------------------------------------- */
EXECUTE AS USER = 'app_sgs_user';
SELECT entity_name = 'hangfire', permission_name FROM fn_my_permissions('hangfire', 'SCHEMA');
REVERT;
GO
