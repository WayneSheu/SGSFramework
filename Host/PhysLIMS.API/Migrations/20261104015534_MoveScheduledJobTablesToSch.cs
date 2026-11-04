using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class MoveScheduledJobTablesToSch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 排程管理改為外掛模組 SGS.Modules.SCH（schema sch）：資料表移出 core，並將權限碼 SYSTEM.* 改為 SCH.*。
            // 與外掛 migration SCHDB_Initial 規則相同（存在即移轉、不重建），兩者執行順序不影響結果。
            migrationBuilder.Sql("""
                IF SCHEMA_ID(N'sch') IS NULL EXEC(N'CREATE SCHEMA [sch]');
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[core].[ScheduledJobRuns]', N'U') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[sch].[ScheduledJobRuns]', N'U') IS NULL
                        ALTER SCHEMA [sch] TRANSFER [core].[ScheduledJobRuns];
                    ELSE
                    BEGIN
                        -- 外掛已先建立空表：合併舊紀錄後移除 core 表
                        INSERT INTO [sch].[ScheduledJobRuns] ([Id], [JobId], [JobName], [HangfireJobId], [Attempt], [Trigger], [TriggeredBy], [Status], [StartedAt], [FinishedAt], [DurationMs], [Message], [ServerName])
                        SELECT c.[Id], c.[JobId], c.[JobName], c.[HangfireJobId], c.[Attempt], c.[Trigger], c.[TriggeredBy], c.[Status], c.[StartedAt], c.[FinishedAt], c.[DurationMs], c.[Message], c.[ServerName]
                        FROM [core].[ScheduledJobRuns] c
                        WHERE NOT EXISTS (SELECT 1 FROM [sch].[ScheduledJobRuns] s WHERE s.[Id] = c.[Id] OR (s.[HangfireJobId] = c.[HangfireJobId] AND s.[Attempt] = c.[Attempt]));
                        DROP TABLE [core].[ScheduledJobRuns];
                    END
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[core].[ScheduledJobSettings]', N'U') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[sch].[ScheduledJobSettings]', N'U') IS NULL
                        ALTER SCHEMA [sch] TRANSFER [core].[ScheduledJobSettings];
                    ELSE
                    BEGIN
                        INSERT INTO [sch].[ScheduledJobSettings] ([JobId], [IsPaused], [PauseReason], [Cron], [TimeZone], [CreatedBy], [CreatedAtUtc], [UpdatedBy], [UpdatedAtUtc])
                        SELECT c.[JobId], c.[IsPaused], c.[PauseReason], c.[Cron], c.[TimeZone], c.[CreatedBy], c.[CreatedAtUtc], c.[UpdatedBy], c.[UpdatedAtUtc]
                        FROM [core].[ScheduledJobSettings] c
                        WHERE NOT EXISTS (SELECT 1 FROM [sch].[ScheduledJobSettings] s WHERE s.[JobId] = c.[JobId]);
                        DROP TABLE [core].[ScheduledJobSettings];
                    END
                END
                """);

            // 權限碼改名：角色功能權限（位元遮罩以控制器為單位，動作宣告順序不變故位元沿用）、角色 Claim、權限 / 控制器中繼資料與選單節點。
            // 控制器中繼資料改歸 SGS.Modules.SCH，模組註冊時即更新原紀錄（保留 Id 與手動停用設定）。
            migrationBuilder.Sql("""
                UPDATE r
                SET [permission_key] = N'SCH.' + SUBSTRING(r.[permission_key], 8, 200)
                FROM [core].[Role_Global_Permissions] r
                WHERE r.[permission_key] IN (N'SYSTEM.SCHEDULER', N'SYSTEM.DATABASEMAINTENANCE')
                  AND NOT EXISTS (SELECT 1 FROM [core].[Role_Global_Permissions] n
                                  WHERE n.[role_id] = r.[role_id] AND n.[permission_key] = N'SCH.' + SUBSTRING(r.[permission_key], 8, 200));

                UPDATE [core].[AspNetRoleClaims]
                SET [ClaimValue] = N'SCH.' + SUBSTRING([ClaimValue], 8, 200)
                WHERE [ClaimType] = N'Permission'
                  AND ([ClaimValue] LIKE N'SYSTEM.SCHEDULER.%' OR [ClaimValue] LIKE N'SYSTEM.DATABASEMAINTENANCE.%');

                UPDATE [core].[PermissionMetadata]
                SET [PermissionKey] = N'SCH.' + SUBSTRING([PermissionKey], 8, 200), [ModuleName] = N'SGS.Modules.SCH', [ModuleTitle] = N'排程管理'
                WHERE [PermissionKey] LIKE N'SYSTEM.SCHEDULER.%' OR [PermissionKey] LIKE N'SYSTEM.DATABASEMAINTENANCE.%';

                UPDATE [core].[ControllerMetadatas]
                SET [PermissionKey] = N'SCH.' + SUBSTRING([PermissionKey], 8, 200),
                    [ModuleName] = N'SGS.Modules.SCH', [ModuleTitle] = N'排程管理',
                    [ControllerTypeName] = REPLACE([ControllerTypeName], N'SGSFramework.Scheduler.Controllers', N'SGS.Modules.SCH.Controllers'),
                    [Path] = CASE WHEN [Path] = N'/sgsframework.system/scheduler' THEN N'/sgs.modules.sch/scheduler' ELSE [Path] END
                WHERE [ControllerTypeName] LIKE N'SGSFramework.Scheduler.Controllers.%';

                UPDATE [core].[MenuItems]
                SET [Key] = N'SCH.' + SUBSTRING([Key], 8, 200),
                    [PermissionKey] = N'SCH.' + SUBSTRING([PermissionKey], 8, 200),
                    [ModuleName] = N'SGS.Modules.SCH',
                    [Route] = CASE WHEN [Route] = N'/sgsframework.system/scheduler' THEN N'/sgs.modules.sch/scheduler' ELSE [Route] END
                WHERE [Key] LIKE N'SYSTEM.SCHEDULER.%' OR [Key] LIKE N'SYSTEM.DATABASEMAINTENANCE.%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[sch].[ScheduledJobRuns]', N'U') IS NOT NULL AND OBJECT_ID(N'[core].[ScheduledJobRuns]', N'U') IS NULL
                    ALTER SCHEMA [core] TRANSFER [sch].[ScheduledJobRuns];
                IF OBJECT_ID(N'[sch].[ScheduledJobSettings]', N'U') IS NOT NULL AND OBJECT_ID(N'[core].[ScheduledJobSettings]', N'U') IS NULL
                    ALTER SCHEMA [core] TRANSFER [sch].[ScheduledJobSettings];
                """);

            migrationBuilder.Sql("""
                UPDATE [core].[Role_Global_Permissions] SET [permission_key] = N'SYSTEM.' + SUBSTRING([permission_key], 5, 200)
                WHERE [permission_key] IN (N'SCH.SCHEDULER', N'SCH.DATABASEMAINTENANCE');
                UPDATE [core].[AspNetRoleClaims] SET [ClaimValue] = N'SYSTEM.' + SUBSTRING([ClaimValue], 5, 200)
                WHERE [ClaimType] = N'Permission' AND ([ClaimValue] LIKE N'SCH.SCHEDULER.%' OR [ClaimValue] LIKE N'SCH.DATABASEMAINTENANCE.%');
                UPDATE [core].[PermissionMetadata] SET [PermissionKey] = N'SYSTEM.' + SUBSTRING([PermissionKey], 5, 200), [ModuleName] = N'SGSFramework.System', [ModuleTitle] = N'系統管理'
                WHERE [PermissionKey] LIKE N'SCH.SCHEDULER.%' OR [PermissionKey] LIKE N'SCH.DATABASEMAINTENANCE.%';
                UPDATE [core].[ControllerMetadatas]
                SET [PermissionKey] = N'SYSTEM.' + SUBSTRING([PermissionKey], 5, 200), [ModuleName] = N'SGSFramework.System', [ModuleTitle] = N'系統管理',
                    [ControllerTypeName] = REPLACE([ControllerTypeName], N'SGS.Modules.SCH.Controllers', N'SGSFramework.Scheduler.Controllers'),
                    [Path] = CASE WHEN [Path] = N'/sgs.modules.sch/scheduler' THEN N'/sgsframework.system/scheduler' ELSE [Path] END
                WHERE [ControllerTypeName] LIKE N'SGS.Modules.SCH.Controllers.%';
                UPDATE [core].[MenuItems]
                SET [Key] = N'SYSTEM.' + SUBSTRING([Key], 5, 200), [PermissionKey] = N'SYSTEM.' + SUBSTRING([PermissionKey], 5, 200), [ModuleName] = N'SGSFramework.System',
                    [Route] = CASE WHEN [Route] = N'/sgs.modules.sch/scheduler' THEN N'/sgsframework.system/scheduler' ELSE [Route] END
                WHERE [Key] LIKE N'SCH.SCHEDULER.%' OR [Key] LIKE N'SCH.DATABASEMAINTENANCE.%';
                """);
        }
    }
}
