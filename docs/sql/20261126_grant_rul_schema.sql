/* =============================================================================
   PhysLIMS 動態規則引擎外掛模組 (SGS.Modules.RUL) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 規則、變更申請、版本與測試案例位於 [rul] Schema（模組 RULDB_Initial 於 API 啟動時建立）
   - 部署帳號 (deploy_sgs_user) 於 API 啟動時建立資料表；RulMigrationService 亦會嘗試授權執行期帳號，
     若部署帳號無權授權（Schema 由 dbo 擁有），請先執行本腳本再啟動 API
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'rul') IS NULL
    EXEC (N'CREATE SCHEMA [rul] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：規則、變更申請、版本與測試案例資料
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[rul] TO [app_sgs_user];
GO

-- 部署帳號：建立 / 異動資料表
GRANT ALTER, CONTROL ON SCHEMA::[rul] TO [deploy_sgs_user];
GO
