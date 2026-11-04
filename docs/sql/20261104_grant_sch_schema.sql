/* =============================================================================
   PhysLIMS 排程管理外掛模組 (SGS.Modules.SCH) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 排程設定 / 執行紀錄由 core 移至 [sch] Schema（Host migration MoveScheduledJobTablesToSch 與外掛 SCHDB_Initial 以 ALTER SCHEMA TRANSFER 搬移）
   - 部署帳號 (deploy_sgs_user) 於 API 啟動時建立 / 移轉資料表；SchMigrationService 亦會嘗試授權執行期帳號，
     若部署帳號無權授權（Schema 由 dbo 擁有），請先執行本腳本再啟動 API
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'sch') IS NULL
    EXEC (N'CREATE SCHEMA [sch] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：排程設定、執行紀錄、日誌清除 (DELETE) 與模組稽核
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[sch] TO [app_sgs_user];
GO

-- 部署帳號：建立 / 移轉資料表（ALTER SCHEMA TRANSFER 需來源 core 與目的 sch 的 CONTROL）
GRANT ALTER, CONTROL ON SCHEMA::[sch] TO [deploy_sgs_user];
GO
