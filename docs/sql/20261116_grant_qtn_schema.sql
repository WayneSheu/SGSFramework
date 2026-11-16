/* =============================================================================
   PhysLIMS 報價管理外掛模組 (SGS.Modules.QTN) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 客戶、角色費率（含機密成本單價）、標準 WBS 工時庫、商機、報價版本、審批與稽核軌跡、CR 評估、結案復盤
     位於 [qtn] Schema（模組 QTNDB_Initial 於 API 啟動時建立）
   - 部署帳號 (deploy_sgs_user) 於 API 啟動時建立資料表；QtnMigrationService 亦會嘗試授權執行期帳號，
     若部署帳號無權授權（Schema 由 dbo 擁有），請先執行本腳本再啟動 API
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'qtn') IS NULL
    EXEC (N'CREATE SCHEMA [qtn] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：報價業務資料
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[qtn] TO [app_sgs_user];
GO

-- 稽核軌跡只增不改：執行期帳號不得修改或刪除 ApprovalLogs（資料表建立後執行；首次部署可於 API 啟動後再補跑本段）
IF OBJECT_ID(N'[qtn].[ApprovalLogs]', N'U') IS NOT NULL
    DENY UPDATE, DELETE ON OBJECT::[qtn].[ApprovalLogs] TO [app_sgs_user];
GO

-- 部署帳號：建立 / 異動資料表
GRANT ALTER, CONTROL ON SCHEMA::[qtn] TO [deploy_sgs_user];
GO
