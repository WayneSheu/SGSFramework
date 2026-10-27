/* =============================================================================
   PhysLIMS 資料庫帳號最小權限設定 (ISO 27001 A.8.2 / A.8.3)
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   適用版本：自 2026-10-27 起，執行期一律使用 app_sgs_user，
             僅啟動時 Migration 經由 MigrationConnection 暫時改用 deploy_sgs_user。

   權限依據 (程式實際行為)：
   - 執行期資料存取：EF Core 對 core / org 兩個 Schema 的 SELECT / INSERT / UPDATE / DELETE
     (含 Outbox 的 UPDATE ... OUTPUT、Token 儲存的 FromSqlRaw、稽核與系統日誌的 SqlBulkCopy)
   - SqlBulkCopy 已指定 CheckConstraints + FireTriggers，故只需 INSERT，不需 ALTER TABLE
   - 資料庫結構檢視 (ERD / 結構匯出) 讀取 sys.tables、sys.columns、sys.extended_properties
   - Ledger 驗證呼叫 sys.sp_generate_database_ledger_digest 與 sys.sp_verify_database_ledger
   ============================================================================= */
USE [PhysLIMSDB];
GO

/* -----------------------------------------------------------------------------
   0. 移除過大權限 (若過去曾授予，執行前請先確認)
   ----------------------------------------------------------------------------- */
-- ALTER ROLE [db_owner]    DROP MEMBER [app_sgs_user];
-- ALTER ROLE [db_ddladmin] DROP MEMBER [app_sgs_user];

/* -----------------------------------------------------------------------------
   1. app_sgs_user：執行期帳號 (DefaultConnection)
   ----------------------------------------------------------------------------- */
-- 1.1 資料存取 (以 Schema 為單位授權，Migration 新增的資料表自動涵蓋)
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[core] TO [app_sgs_user];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[org]  TO [app_sgs_user];

-- 1.2 中繼資料讀取：資料庫結構檢視功能 (含資料行說明 MS_Description)
GRANT VIEW DEFINITION ON SCHEMA::[core] TO [app_sgs_user];
GRANT VIEW DEFINITION ON SCHEMA::[org]  TO [app_sgs_user];

-- 1.3 Ledger 完整性驗證
GRANT GENERATE LEDGER DIGEST TO [app_sgs_user];
GRANT VIEW LEDGER CONTENT    TO [app_sgs_user];

-- 1.4 明確不授予：CREATE TABLE / ALTER / CONTROL / db_ddladmin / db_owner
--     (執行期若出現權限錯誤，表示有程式在執行期做 DDL，應修正程式而非放寬權限)
GO

/* -----------------------------------------------------------------------------
   2. deploy_sgs_user：部署帳號 (MigrationConnection，僅啟動時 Migration 使用)
   ----------------------------------------------------------------------------- */
ALTER ROLE [db_ddladmin]   ADD MEMBER [deploy_sgs_user];  -- CREATE SCHEMA / TABLE / INDEX / VIEW
ALTER ROLE [db_datareader] ADD MEMBER [deploy_sgs_user];  -- Migration 讀取 __EFMigrationsHistory 與資料移轉
ALTER ROLE [db_datawriter] ADD MEMBER [deploy_sgs_user];  -- Migration 內的資料移轉與歷史紀錄寫入
GRANT ENABLE LEDGER TO [deploy_sgs_user];                 -- 建立 Ledger (防篡改) 資料表
GO

/* -----------------------------------------------------------------------------
   3. 一次性資料庫設定 (DBA 執行；Ledger 驗證需要快照隔離，執行期帳號無權自行開啟)
   ----------------------------------------------------------------------------- */
IF (SELECT snapshot_isolation_state FROM sys.databases WHERE name = DB_NAME()) = 0
    ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

/* -----------------------------------------------------------------------------
   4. 驗證：以 app_sgs_user 身分檢視實際權限 (應無 CREATE TABLE / ALTER)
   ----------------------------------------------------------------------------- */
EXECUTE AS USER = 'app_sgs_user';
SELECT permission_name FROM fn_my_permissions(NULL, 'DATABASE') ORDER BY permission_name;
SELECT entity_name = 'core', permission_name FROM fn_my_permissions('core', 'SCHEMA')
UNION ALL
SELECT entity_name = 'org',  permission_name FROM fn_my_permissions('org',  'SCHEMA');
REVERT;
GO
