/* =============================================================================
   PhysLIMS 報表外掛模組 (SGS.Modules.RPT) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 報表類別授權、存取紀錄 (Ledger) 等資料表位於 [rpt] Schema，由外掛 RptMigrationService 以部署帳號 (deploy_sgs_user) 建立
   - RptMigrationService 亦會嘗試授權執行期帳號，若部署帳號無權授權（例如 Schema 由 dbo 擁有），
     請先執行本腳本再啟動 API
   - Append-only Ledger 資料表本身即拒絕 UPDATE / DELETE，Schema 層級授權不影響其防篡改特性
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'rpt') IS NULL
    EXEC (N'CREATE SCHEMA [rpt] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：報表類別授權設定與存取紀錄
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[rpt] TO [app_sgs_user];
GO

-- 部署帳號：建立 / 異動資料表（含 Ledger 資料表，需 ENABLE LEDGER，見 20261027 腳本）
GRANT ALTER, CONTROL ON SCHEMA::[rpt] TO [deploy_sgs_user];
GO
