/* =============================================================================
   PhysLIMS 日誌庫年度換庫專用帳號 rotate_sgs_user 授權（最小權限）
   執行身分：DBA (sysadmin)    對象：SQL Server 執行個體（日誌庫 PhysLIMSLogDB 與其冷庫）

   為什麼要專用帳號：
     年度換庫（備份熱庫 → 還原為唯讀冷庫 → 刪除舊熱庫 → 建立新熱庫）需要建立 / 刪除資料庫，
     這些權限不可併入備份帳號 backup_sgs_user（該帳號每天由排程備份作業使用，只應能備份）、
     執行期帳號 app_sgs_user（僅 SELECT / INSERT）或部署帳號 deploy_sgs_user（僅 log Schema 的 DDL）。

   授予的權限（皆為最小必要，不使用 dbcreator / sysadmin）：
     1) 伺服器層級：CREATE ANY DATABASE
        - 僅能「建立新資料庫」與「還原成新資料庫」；建立者即該庫擁有者（db_owner），
          所以換庫產生的冷庫與新熱庫自動歸 rotate_sgs_user 管理，下次換庫可直接刪除。
        - 不能修改或刪除其他資料庫（含主庫 PhysLIMSDB）。
     2) 現行熱庫 PhysLIMSLogDB：建立使用者並加入 db_owner
        - 現行熱庫由 DBA 建立（擁有者非 rotate_sgs_user），需 db_owner 才能備份、改為限制使用者模式與刪除。
        - 首次換庫完成後，新熱庫由 rotate_sgs_user 建立（擁有者即本帳號），之後不需再手動授權。
   帳號不具有：sysadmin、dbcreator、主庫 PhysLIMSDB 的任何權限、ALTER ANY LOGIN。

   前置：伺服器需已有登入 rotate_sgs_user（密碼以強密碼自行建立，勿寫入版控）。
     CREATE LOGIN [rotate_sgs_user] WITH PASSWORD = N'<強密碼>', CHECK_POLICY = ON, DEFAULT_DATABASE = [master];
   連線字串（以環境變數注入，勿寫入版控；須與 LogConnection / LogMigrationConnection / BackupConnection 使用不同帳號）：
     PersistentSettings__ConnectionStrings__RotationConnection
   另外：SQL Server「服務帳號」需對備份目錄有寫入權限（預設備份目錄必定可寫）；可由設定 LogRotation:BackupDirectory 指定其他目錄。

   可重複執行。
   ============================================================================= */
USE [master];
GO

SET NOCOUNT ON;

DECLARE @rotateLogin sysname = N'rotate_sgs_user';   -- 換庫登入帳號
DECLARE @hotDb       sysname = N'PhysLIMSLogDB';     -- 現行熱庫名稱
DECLARE @sql nvarchar(max);

IF SUSER_ID(@rotateLogin) IS NULL
BEGIN
    RAISERROR(N'伺服器尚無登入 %s，請先以強密碼建立（見檔頭說明）後重新執行。', 16, 1, @rotateLogin);
    RETURN;
END;

IF IS_SRVROLEMEMBER(N'sysadmin', @rotateLogin) = 1
    PRINT N'警告：' + @rotateLogin + N' 是 sysadmin 成員，違反最小權限，請移除（ALTER SERVER ROLE [sysadmin] DROP MEMBER ...）。';

IF DB_ID(@hotDb) IS NULL
    PRINT N'提示：熱庫 ' + @hotDb + N' 不存在，僅授予伺服器層級權限；建庫後請重新執行本腳本。';

-- 1. 伺服器層級：僅能建立 / 還原為新資料庫
SET @sql = N'GRANT CREATE ANY DATABASE TO ' + QUOTENAME(@rotateLogin) + N';';
EXEC (@sql);

-- 2. 現行熱庫：使用者 + db_owner（備份、限制使用者模式、刪除舊熱庫）
IF DB_ID(@hotDb) IS NOT NULL
BEGIN
    SET @sql = N'USE ' + QUOTENAME(@hotDb) + N';
        IF DATABASE_PRINCIPAL_ID(@login) IS NULL
        BEGIN
            DECLARE @c nvarchar(400) = N''CREATE USER '' + QUOTENAME(@login) + N'' FOR LOGIN '' + QUOTENAME(@login) + N'';'';
            EXEC (@c);
        END;
        IF IS_ROLEMEMBER(N''db_owner'', @login) = 0
        BEGIN
            DECLARE @r nvarchar(400) = N''ALTER ROLE [db_owner] ADD MEMBER '' + QUOTENAME(@login) + N'';'';
            EXEC (@r);
        END;';
    EXEC sp_executesql @sql, N'@login sysname', @login = @rotateLogin;
END;
GO

/* 驗證 1：預期 has_create_any_database = 1、is_sysadmin = 0、is_dbcreator = 0 */
SELECT
    IS_SRVROLEMEMBER(N'sysadmin',  N'rotate_sgs_user') AS is_sysadmin,
    IS_SRVROLEMEMBER(N'dbcreator', N'rotate_sgs_user') AS is_dbcreator,
    (SELECT COUNT(*) FROM sys.server_permissions p
      JOIN sys.server_principals sp ON sp.principal_id = p.grantee_principal_id
      WHERE sp.name = N'rotate_sgs_user' AND p.permission_name = N'CREATE ANY DATABASE' AND p.state = 'G') AS has_create_any_database;
GO

/* 驗證 2：預期熱庫中 rotate_sgs_user 為 db_owner（回傳 1）；主庫不應有此使用者（回傳 NULL）。資料庫名稱請與上方 @hotDb 一致 */
USE [PhysLIMSLogDB];
SELECT IS_ROLEMEMBER(N'db_owner', N'rotate_sgs_user') AS hot_db_owner;
GO
USE [PhysLIMSDB];
SELECT DATABASE_PRINCIPAL_ID(N'rotate_sgs_user') AS main_db_user_should_be_null;
GO
