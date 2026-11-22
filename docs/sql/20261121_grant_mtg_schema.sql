/* =============================================================================
   PhysLIMS 會議助理外掛模組 (SGS.Modules.MTG) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 部署帳號 (deploy_sgs_user) 於 API 啟動時建立 mtg Schema 與資料表；MtgMigrationService 亦會嘗試授權執行期帳號，
     若部署帳號無權授權（Schema 由 dbo 擁有），請先執行本腳本再啟動 API
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'mtg') IS NULL
    EXEC (N'CREATE SCHEMA [mtg] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：會議、逐字稿、紀要、場景模板、術語表與模組稽核
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[mtg] TO [app_sgs_user];
GO

-- 部署帳號：建立資料表與 __EFMigrationsHistory
GRANT ALTER, CONTROL ON SCHEMA::[mtg] TO [deploy_sgs_user];
GO
