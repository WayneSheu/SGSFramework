/* =============================================================================
   PhysLIMS 專案管理外掛模組 (SGS.Modules.PMS) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 專案、成員、卡控節點、交付物、專案代號保留表位於 [pms] Schema（模組 PMSDB_Initial 於 API 啟動時建立）
   - 部署帳號 (deploy_sgs_user) 於 API 啟動時建立資料表；PmsMigrationService 亦會嘗試授權執行期帳號，
     若部署帳號無權授權（Schema 由 dbo 擁有），請先執行本腳本再啟動 API
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   - 專案代號（pms.ProjectCodeReservations）永不重用：不得以人工 DELETE 清除
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'pms') IS NULL
    EXEC (N'CREATE SCHEMA [pms] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：專案資料與模組稽核
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[pms] TO [app_sgs_user];
GO

-- 部署帳號：建立 / 異動資料表
GRANT ALTER, CONTROL ON SCHEMA::[pms] TO [deploy_sgs_user];
GO
