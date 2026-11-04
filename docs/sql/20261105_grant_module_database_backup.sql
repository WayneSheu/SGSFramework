/* =============================================================================
   PhysLIMS 外掛模組自有資料庫備份權限（排程作業 database-backup-full-{模組}）
   執行身分：DBA (sysadmin 或各資料庫 db_owner)
   - 模組資料庫備份沿用主庫的備份登入 backup_sgs_user（BackupConnection），僅切換資料庫，
     因此該登入須在每個模組資料庫建立使用者並加入 db_backupoperator
   - 前置：20261028_grant_scheduler_hangfire_backup.sql 已建立 backup_sgs_user 登入，
     並於 master 授予 CREATE DATABASE（RESTORE VERIFYONLY 需要）
   - 模組資料庫須與主庫位於同一 SQL Server 執行個體
   新增其他自有資料庫的模組時，複製第 1 段並修改資料庫名稱
   ============================================================================= */

/* -----------------------------------------------------------------------------
   1. 日誌外掛 SGS.Modules.LOG：PhysLIMSLogDB（作業 database-backup-full-log / database-backup-differential-log）
   ----------------------------------------------------------------------------- */
USE [PhysLIMSLogDB];
GO

IF DATABASE_PRINCIPAL_ID(N'backup_sgs_user') IS NULL
    CREATE USER [backup_sgs_user] FOR LOGIN [backup_sgs_user];
GO

ALTER ROLE [db_backupoperator] ADD MEMBER [backup_sgs_user];
GO
