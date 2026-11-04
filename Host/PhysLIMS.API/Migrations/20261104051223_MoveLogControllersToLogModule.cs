using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class MoveLogControllersToLogModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 日誌管理歸入外掛模組 SGS.Modules.LOG：系統 / 資安日誌查詢、資料日誌、告警規則、總帳驗證四個控制器改歸 LOG 模組，
            // 權限碼 SYSTEM.* 改為 LOG.*（僅更新資料，不改資料表結構）。與 SCH 遷移（MoveScheduledJobTablesToSch）規則相同：
            // 角色功能權限的位元遮罩以控制器為單位，動作宣告順序不變故位元沿用；控制器中繼資料改歸 SGS.Modules.LOG，
            // 模組註冊時即更新原紀錄（保留 Id 與手動停用設定）。API 路由與選單頁面路徑不變。
            // SUBSTRING(key, 8, 200)：略過 "SYSTEM." 共 7 個字元。
            migrationBuilder.Sql("""
                UPDATE r
                SET [permission_key] = N'LOG.' + SUBSTRING(r.[permission_key], 8, 200)
                FROM [core].[Role_Global_Permissions] r
                WHERE r.[permission_key] IN (N'SYSTEM.LOGMANAGER', N'SYSTEM.AUDITLOG', N'SYSTEM.ALERTRULE', N'SYSTEM.LEDGERVERIFICATION')
                  AND NOT EXISTS (SELECT 1 FROM [core].[Role_Global_Permissions] n
                                  WHERE n.[role_id] = r.[role_id] AND n.[permission_key] = N'LOG.' + SUBSTRING(r.[permission_key], 8, 200));

                UPDATE [core].[AspNetRoleClaims]
                SET [ClaimValue] = N'LOG.' + SUBSTRING([ClaimValue], 8, 200)
                WHERE [ClaimType] = N'Permission'
                  AND ([ClaimValue] LIKE N'SYSTEM.LOGMANAGER.%' OR [ClaimValue] LIKE N'SYSTEM.AUDITLOG.%'
                       OR [ClaimValue] LIKE N'SYSTEM.ALERTRULE.%' OR [ClaimValue] LIKE N'SYSTEM.LEDGERVERIFICATION.%');

                UPDATE [core].[PermissionMetadata]
                SET [PermissionKey] = N'LOG.' + SUBSTRING([PermissionKey], 8, 200), [ModuleName] = N'SGS.Modules.LOG', [ModuleTitle] = N'日誌管理'
                WHERE [PermissionKey] LIKE N'SYSTEM.LOGMANAGER.%' OR [PermissionKey] LIKE N'SYSTEM.AUDITLOG.%'
                   OR [PermissionKey] LIKE N'SYSTEM.ALERTRULE.%' OR [PermissionKey] LIKE N'SYSTEM.LEDGERVERIFICATION.%';

                UPDATE [core].[ControllerMetadatas]
                SET [PermissionKey] = N'LOG.' + SUBSTRING([PermissionKey], 8, 200),
                    [ModuleName] = N'SGS.Modules.LOG', [ModuleTitle] = N'日誌管理',
                    [ControllerTypeName] = REPLACE(REPLACE([ControllerTypeName],
                        N'SGSFramework.SystemLog.Controllers.V1.', N'SGS.Modules.LOG.Controllers.V1.'),
                        N'SGSFramework.VerifyLedger.Controllers.V1.', N'SGS.Modules.LOG.Controllers.V1.')
                WHERE [ControllerTypeName] IN (
                    N'SGSFramework.SystemLog.Controllers.V1.LogManagerController',
                    N'SGSFramework.SystemLog.Controllers.V1.AuditLogManagementController',
                    N'SGSFramework.SystemLog.Controllers.V1.AlertRuleController',
                    N'SGSFramework.VerifyLedger.Controllers.V1.LedgerVerificationController');

                UPDATE [core].[MenuItems]
                SET [Key] = N'LOG.' + SUBSTRING([Key], 8, 200),
                    [PermissionKey] = N'LOG.' + SUBSTRING([PermissionKey], 8, 200),
                    [ModuleName] = N'SGS.Modules.LOG'
                WHERE [Key] LIKE N'SYSTEM.LOGMANAGER.%' OR [Key] LIKE N'SYSTEM.AUDITLOG.%'
                   OR [Key] LIKE N'SYSTEM.ALERTRULE.%' OR [Key] LIKE N'SYSTEM.LEDGERVERIFICATION.%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 還原：LOG.* 權限碼與控制器歸屬回到 SYSTEM / SGSFramework.System（SUBSTRING(key, 5, 200) 略過 "LOG." 共 4 個字元）
            migrationBuilder.Sql("""
                UPDATE [core].[Role_Global_Permissions] SET [permission_key] = N'SYSTEM.' + SUBSTRING([permission_key], 5, 200)
                WHERE [permission_key] IN (N'LOG.LOGMANAGER', N'LOG.AUDITLOG', N'LOG.ALERTRULE', N'LOG.LEDGERVERIFICATION');

                UPDATE [core].[AspNetRoleClaims] SET [ClaimValue] = N'SYSTEM.' + SUBSTRING([ClaimValue], 5, 200)
                WHERE [ClaimType] = N'Permission'
                  AND ([ClaimValue] LIKE N'LOG.LOGMANAGER.%' OR [ClaimValue] LIKE N'LOG.AUDITLOG.%'
                       OR [ClaimValue] LIKE N'LOG.ALERTRULE.%' OR [ClaimValue] LIKE N'LOG.LEDGERVERIFICATION.%');

                UPDATE [core].[PermissionMetadata]
                SET [PermissionKey] = N'SYSTEM.' + SUBSTRING([PermissionKey], 5, 200), [ModuleName] = N'SGSFramework.System', [ModuleTitle] = N'系統管理'
                WHERE [PermissionKey] LIKE N'LOG.LOGMANAGER.%' OR [PermissionKey] LIKE N'LOG.AUDITLOG.%'
                   OR [PermissionKey] LIKE N'LOG.ALERTRULE.%' OR [PermissionKey] LIKE N'LOG.LEDGERVERIFICATION.%';

                UPDATE [core].[ControllerMetadatas]
                SET [PermissionKey] = N'SYSTEM.' + SUBSTRING([PermissionKey], 5, 200),
                    [ModuleName] = N'SGSFramework.System', [ModuleTitle] = N'系統管理',
                    [ControllerTypeName] = CASE WHEN [ControllerTypeName] = N'SGS.Modules.LOG.Controllers.V1.LedgerVerificationController'
                        THEN N'SGSFramework.VerifyLedger.Controllers.V1.LedgerVerificationController'
                        ELSE REPLACE([ControllerTypeName], N'SGS.Modules.LOG.Controllers.V1.', N'SGSFramework.SystemLog.Controllers.V1.') END
                WHERE [ControllerTypeName] IN (
                    N'SGS.Modules.LOG.Controllers.V1.LogManagerController',
                    N'SGS.Modules.LOG.Controllers.V1.AuditLogManagementController',
                    N'SGS.Modules.LOG.Controllers.V1.AlertRuleController',
                    N'SGS.Modules.LOG.Controllers.V1.LedgerVerificationController');

                UPDATE [core].[MenuItems]
                SET [Key] = N'SYSTEM.' + SUBSTRING([Key], 5, 200), [PermissionKey] = N'SYSTEM.' + SUBSTRING([PermissionKey], 5, 200),
                    [ModuleName] = N'SGSFramework.System'
                WHERE [Key] LIKE N'LOG.LOGMANAGER.%' OR [Key] LIKE N'LOG.AUDITLOG.%'
                   OR [Key] LIKE N'LOG.ALERTRULE.%' OR [Key] LIKE N'LOG.LEDGERVERIFICATION.%';
                """);
        }
    }
}
