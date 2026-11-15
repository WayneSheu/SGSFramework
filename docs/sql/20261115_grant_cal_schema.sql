/* =============================================================================
   PhysLIMS 行事曆管理外掛模組 (SGS.Modules.CAL) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 假日與補班日、日曆、日曆活動位於 [cal] Schema（模組 CALDB_Initial 於 API 啟動時建立）
   - 部署帳號 (deploy_sgs_user) 於 API 啟動時建立資料表；CalMigrationService 亦會嘗試授權執行期帳號，
     若部署帳號無權授權（Schema 由 dbo 擁有），請先執行本腳本再啟動 API
   - CALDB_Initial 會一次性把 [pms].[Holidays] 複製到 [cal].[Holidays]（只在 pms 表存在時；已存在的日期不覆蓋），
     因此部署帳號需要能讀取 pms.Holidays（PMS 的 20261113_grant_pms_schema.sql 已授與 CONTROL，可略過下方 GRANT）
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'cal') IS NULL
    EXEC (N'CREATE SCHEMA [cal] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：假日、日曆與活動資料
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[cal] TO [app_sgs_user];
GO

-- 部署帳號：建立 / 異動資料表
GRANT ALTER, CONTROL ON SCHEMA::[cal] TO [deploy_sgs_user];
GO

-- 部署帳號：一次性讀取 PMS 舊假日資料（pms.Holidays 存在時）
IF OBJECT_ID(N'[pms].[Holidays]', N'U') IS NOT NULL
    GRANT SELECT ON OBJECT::[pms].[Holidays] TO [deploy_sgs_user];
GO
