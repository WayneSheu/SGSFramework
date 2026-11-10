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

   注意事項
   1. 前置：app_sgs_user / deploy_sgs_user / backup_sgs_user 三個「伺服器登入」須已存在
      （建立資料庫使用者需對應登入；backup_sgs_user 由 20261028_grant_scheduler_hangfire_backup.sql 建立）。
      API 只建立資料庫使用者，不建立登入，也不會設定密碼
   2. 僅在資料庫「由部署帳號建立」時才會自動完成使用者與授權：建立者即該庫的 dbo，才有權建立使用者。
      資料庫若已先由 DBA 手動建立（部署帳號不是 dbo），啟動時這些步驟只會記警告，缺少的帳號需由 DBA 補授權：
      執行 20261105_create_log_database.sql（含備份帳號），或單獨執行 20261105_grant_module_database_backup.sql
      （備份帳號症狀：排程備份回報「備份帳號無法存取資料庫 PhysLIMSLogDB」）
   3. 日誌庫重建（例如開發環境重置）後，先確認本授權仍在（登入 / 權限在 master），再重啟 API 讓它重新建庫與授權；
      建庫後以文末「啟動後驗證」確認三個帳號都已建立
   4. 資料庫擁有者（dbo）為建立它的部署帳號；若政策要求由 DBA 持有，建庫後可執行
      ALTER AUTHORIZATION ON DATABASE::[PhysLIMSLogDB] TO [sa]。改擁有者後部署帳號即失去 dbo 權限，
      須先執行 20261105_create_log_database.sql（授予部署帳號 log schema 的 ALTER / CONTROL、CREATE TABLE 與 ENABLE LEDGER），
      否則啟動時無法建立資料表
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

/* 啟動後驗證（API 首次啟動並建庫後執行；預期三個帳號各一列，備份帳號 backup_operator = 1）
   登入帳號名稱若改為專用帳號，請一併修改。

USE [PhysLIMSLogDB];
SELECT name AS database_user,
       IS_ROLEMEMBER(N'db_backupoperator', name) AS backup_operator,
       HAS_PERMS_BY_NAME(N'log', N'SCHEMA', N'INSERT') AS can_insert_log
FROM sys.database_principals
WHERE name IN (N'app_sgs_user', N'deploy_sgs_user', N'backup_sgs_user');
*/
