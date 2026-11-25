/*
  Migration: 20261124080309_UserLabMappingAssignmentType
  用途：core.UserLabMappings 以 AssignmentType (0=主要,1=兼任,2=支援) 取代 IsPrimary；
        IsPrimary 改為由 AssignmentType 計算的唯讀欄位。
  執行前：1) 請確認連線的資料庫為 PhysLIMSDB（下方 USE）  2) 先備份 core.UserLabMappings
  冪等：已套用（__EFMigrationsHistory 已有紀錄）者再執行不會重複變更。
  任何錯誤（XACT_ABORT）整批回復。
*/
USE [PhysLIMSDB];
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    DROP INDEX [IX_UserLabMappings_User_Active_Primary] ON [core].[UserLabMappings];
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    DROP INDEX [UX_UserLabMappings_OnePrimaryPerUser] ON [core].[UserLabMappings];
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    ALTER TABLE [core].[UserLabMappings] ADD [AssignmentType] int NOT NULL DEFAULT 1;
    DECLARE @description AS sql_variant;
    SET @description = N'歸屬類別：0=主要、1=兼任、2=支援';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'AssignmentType';
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    EXEC(N'UPDATE [core].[UserLabMappings] SET [AssignmentType] = CASE WHEN [IsPrimary] = 1 THEN 0 ELSE 1 END;')
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserLabMappings]') AND [c].[name] = N'AssignmentType');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserLabMappings] DROP CONSTRAINT ' + @var1 + ';');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[core].[UserLabMappings]') AND [c].[name] = N'IsPrimary');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [core].[UserLabMappings] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [core].[UserLabMappings] DROP COLUMN [IsPrimary];
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    EXEC(N'ALTER TABLE [core].[UserLabMappings] ADD [IsPrimary] AS CASE WHEN [AssignmentType] = 0 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END');
    DECLARE @description3 AS sql_variant;
    SET @description3 = N'是否為主要實驗室（由 AssignmentType 計算，唯讀）';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', N'core', 'TABLE', N'UserLabMappings', 'COLUMN', N'IsPrimary';
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    CREATE INDEX [IX_UserLabMappings_User_Active_Primary] ON [core].[UserLabMappings] ([UserId], [IsActive], [AssignmentType]);
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_UserLabMappings_OnePrimaryPerUser] ON [core].[UserLabMappings] ([UserId]) WHERE [AssignmentType] = 0 AND [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [core].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124080309_UserLabMappingAssignmentType'
)
BEGIN
    INSERT INTO [core].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261124080309_UserLabMappingAssignmentType', N'10.0.12');
END;

COMMIT;
GO

