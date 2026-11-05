/* =============================================================================
   PhysLIMS 日誌外掛 (SGS.Modules.LOG) 獨立資料庫自動建置授權
   執行身分：DBA (sysadmin)    對象：伺服器層級（master）
   - 授權後，部署帳號（LogMigrationConnection，預設 deploy_sgs_user）可於 API 啟動時自動：
       1. 建立 PhysLIMSLogDB（不存在時；建立者即為該庫 dbo）
       2. 建立執行期帳號（LogConnection）與備份帳號（BackupConnection）的資料庫使用者，
          授予 log schema SELECT / INSERT、VIEW LEDGER CONTENT，備份帳號加入 db_backupoperator
       3. 建立 log schema、啟用快照隔離，再套用 LOGDB_Initial 等 Migration
   - CREATE ANY DATABASE 只允許建立新資料庫，不授予既有資料庫（含主庫 PhysLIMSDB）任何額外權限
   - 不採用自動建置時，請改執行 20261105_create_log_database.sql 由 DBA 手動建庫
   ============================================================================= */
USE [master];
GO

GRANT CREATE ANY DATABASE TO [deploy_sgs_user];
GO

/* 驗證：預期回傳 1 */
EXECUTE AS LOGIN = N'deploy_sgs_user';
SELECT HAS_PERMS_BY_NAME(NULL, NULL, N'CREATE ANY DATABASE') AS can_create_database;
REVERT;
GO
