/* =============================================================================
   PhysLIMS 檔案管理外掛模組 (SGS.Modules.FMS) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 檔案中繼資料等資料表位於 [fms] Schema，由外掛 FmsMigrationService 以部署帳號 (deploy_sgs_user) 建立
   - FmsMigrationService 亦會嘗試授權執行期帳號，若部署帳號無權授權（例如 Schema 由 dbo 擁有），
     請先執行本腳本再啟動 API
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'fms') IS NULL
    EXEC (N'CREATE SCHEMA [fms] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：檔案中繼資料的查詢、上傳、更新與刪除
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[fms] TO [app_sgs_user];
GO

-- 部署帳號：建立 / 異動資料表
GRANT ALTER, CONTROL ON SCHEMA::[fms] TO [deploy_sgs_user];
GO
