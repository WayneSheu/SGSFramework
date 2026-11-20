/* =============================================================================
   PhysLIMS 操作手冊管理外掛模組 (SGS.Modules.MAN) 資料庫權限設定
   執行身分：DBA (db_owner 或 sysadmin)   對象資料庫：PhysLIMSDB
   - 手冊主題位於 [man] Schema（模組 MANDB_Initial 於 API 啟動時建立）
   - 若部署帳號無權授權（Schema 由 dbo 擁有），請先執行本腳本再啟動 API
   - 延續 20261027_grant_least_privilege_accounts.sql 的最小權限原則
   ============================================================================= */
USE [PhysLIMSDB];
GO

IF SCHEMA_ID(N'man') IS NULL
    EXEC (N'CREATE SCHEMA [man] AUTHORIZATION [dbo]');
GO

-- 執行期帳號：手冊主題資料
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[man] TO [app_sgs_user];
GO

-- 部署帳號：建立 / 異動資料表
GRANT ALTER, CONTROL ON SCHEMA::[man] TO [deploy_sgs_user];
GO
