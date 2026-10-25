using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "UserRoleExpirations",
                schema: "core",
                comment: "過渡期角色（Transition Role）：角色指派的有效期限。調職交接時保留舊角色一段時間，到期由排程自動回收，避免權限累積 (Privilege Creep)。每位使用者在同一角色上最多一筆未處理的期限。");

            migrationBuilder.AlterTable(
                name: "UserResourceGrants",
                schema: "core",
                comment: "使用者資源授權實體，表示特定使用者對特定資源（如功能表項目）的存取權限");

            migrationBuilder.AlterTable(
                name: "UserLabPermissions",
                schema: "core",
                comment: "使用者實驗室專屬 64 位元遮罩權限實體 (對齊 UserLabMapping 型別與稽核標準)");

            migrationBuilder.AlterTable(
                name: "UserLabMappings",
                schema: "core",
                comment: "用戶與實驗室映射（管理用戶與實驗室之間的關聯，包括用戶的主要實驗室和職位標題等信息）");

            migrationBuilder.AlterTable(
                name: "UserGlobalPermissions",
                schema: "core",
                comment: "使用者全域/組織級 64 位元遮罩權限實體 (統一 UserId 為 Guid 型別)");

            migrationBuilder.AlterTable(
                name: "SystemSettings",
                schema: "core",
                comment: "系統配置（管理者可於執行期調整的設定，例如外觀識別、密碼規則）。以「分類鍵 + JSON 值」儲存，新增設定分類時不需異動資料表結構。");

            migrationBuilder.AlterTable(
                name: "SystemSettingChangeLogs",
                schema: "core",
                comment: "系統配置異動歷程（稽核用，僅新增不修改）");

            migrationBuilder.AlterTable(
                name: "SystemLogs",
                schema: "core",
                comment: "系統日誌實體：用於記錄系統運行狀態、事件、錯誤與稽核資訊")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true)
                .OldAnnotation("SqlServer:IsLedger", true)
                .OldAnnotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.AlterTable(
                name: "SecurityLog",
                schema: "core",
                comment: "安全性審計總帳實體模型 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true)
                .OldAnnotation("SqlServer:IsLedger", true)
                .OldAnnotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.AlterTable(
                name: "RoleLabScopes",
                schema: "core",
                comment: "實驗室專屬角色適用的區域實驗室（一個角色可指派多個實驗室）");

            migrationBuilder.AlterTable(
                name: "RoleChangeRequests",
                schema: "core",
                comment: "角色權限變更申請，用於管理角色權限的變更流程。");

            migrationBuilder.AlterTable(
                name: "RoleAuditLogs",
                schema: "core",
                comment: "角色異動歷程（稽核用，僅新增不修改）。記錄角色狀態、設定、權限、成員、AD 群組等所有異動，含操作者、時間與影響人數。");

            migrationBuilder.AlterTable(
                name: "Role_Global_Permissions",
                schema: "core",
                comment: "角色全域/模組級 64 位元遮罩權限實體");

            migrationBuilder.AlterTable(
                name: "RemediationTickets",
                schema: "core",
                comment: "身分補償修復憑證實體：專門用於記錄因資安熔斷（Lockdown）後，允許用戶執行高風險密碼修復或身分重驗的臨時安全 Ticket。");

            migrationBuilder.AlterTable(
                name: "PermissionGrants",
                schema: "core",
                comment: "角色權限授權實體，表示特定角色對特定功能或資源的存取權限。");

            migrationBuilder.AlterTable(
                name: "ModuleMetadatas",
                schema: "core",
                comment: "模組元資料實體，包含模組的基本資訊與狀態");

            migrationBuilder.AlterTable(
                name: "MenuItems",
                schema: "core",
                comment: "動態選單實體 (Domain Entity)");

            migrationBuilder.AlterTable(
                name: "LabManagers",
                schema: "core",
                comment: "區域實驗室主管（以 TenantLabId 對應 ORG 區域實驗室，與 UserLabMapping 相同的鍵值維度）");

            migrationBuilder.AlterTable(
                name: "LabFunctionScopes",
                schema: "core",
                comment: "區域實驗室開放的權限（完整權限鍵，例：ORG.LABORATORY.READ）");

            migrationBuilder.AlterTable(
                name: "LabFunctionPolicies",
                schema: "core",
                comment: "區域實驗室的功能開放政策。IsRestricted = false（或無資料）表示所有功能開放；IsRestricted = true 時，僅 LabFunctionScope 列出的權限於該實驗室生效（對所有使用者與角色的上限，系統管理員除外）。");

            migrationBuilder.AlterTable(
                name: "LabAuditAssignments",
                schema: "core",
                comment: "實驗室稽核指派：指定使用者於起訖期間內對某區域實驗室執行稽核職責。有效期間內，使用者所持有的稽核角色（ApplicationRole.RequiresAuditAssignment）權限於該實驗室生效，且該實驗室會出現在使用者可切換的實驗室清單中；期間外或撤銷後自動失效。紀錄不刪除，保留完整稽核軌跡。");

            migrationBuilder.AlterTable(
                name: "FunctionStatusLogs",
                schema: "core",
                comment: "系統功能啟用 / 停用異動歷程（稽核用，僅新增不修改）");

            migrationBuilder.AlterTable(
                name: "AuditLogs",
                schema: "core",
                comment: "資料稽核日誌實體");

            migrationBuilder.AlterTable(
                name: "AspNetUsers",
                schema: "core",
                comment: "擴充自訂的 Identity 使用者實體");

            migrationBuilder.AlterTable(
                name: "AspNetRoles",
                schema: "core",
                comment: "企業級自訂應用程式角色實體 (支援 ARBAC 與多實驗室隔離)");

            migrationBuilder.AlterTable(
                name: "AccessReviewItems",
                schema: "core",
                comment: "覆核項目：一位使用者在一個角色上的指派");

            migrationBuilder.AlterTable(
                name: "AccessReviewCampaigns",
                schema: "core",
                comment: "定期權限覆核活動 (Access Certification Campaign)。建立時快照當下所有（或指定角色的）成員指派，由覆核者逐一確認「保留」或「回收」。");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserRoleExpirations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "UserRoleExpirations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "core",
                table: "UserRoleExpirations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "設定原因，例如「調任銷售部，業務交接」",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "core",
                table: "UserRoleExpirations",
                type: "datetimeoffset",
                nullable: false,
                comment: "到期時間（UTC），到期後由排程自動解除角色指派",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserRoleExpirations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "建立者",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "UserRoleExpirations",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "UserRoleExpirations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ValidTo",
                schema: "core",
                table: "UserResourceGrants",
                type: "datetimeoffset",
                nullable: true,
                comment: "有效結束時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ValidFrom",
                schema: "core",
                table: "UserResourceGrants",
                type: "datetimeoffset",
                nullable: true,
                comment: "有效起始時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionType",
                schema: "core",
                table: "UserResourceGrants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                comment: "權限類型",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<Guid>(
                name: "MenuItemId",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: false,
                comment: "功能表項目識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "LabId",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: false,
                comment: "實驗室識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "EffectType",
                schema: "core",
                table: "UserResourceGrants",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                comment: "權限效果",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<Guid>(
                name: "DelegatorUserId",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: true,
                comment: "被代理人識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "使用者唯一識別碼",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "RotatedAt",
                schema: "core",
                table: "UserRefreshTokens",
                type: "datetime2",
                nullable: true,
                comment: "上一次成功輪換的時間（用於寬限期計算）",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RiskReason",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(max)",
                nullable: true,
                comment: "記錄風險觸發原因或主動登出/熔斷稽核事由",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReusedTokenCache",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "快取上一次生成的全新 TokenHash，供寬限期內的併發請求直接複用",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastActiveAt",
                schema: "core",
                table: "UserRefreshTokens",
                type: "datetime2",
                nullable: false,
                comment: "該裝置最後一次發起請求或換票的活動時間 (用於 LIFO 被動擠出策略排序)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<bool>(
                name: "IsFrozen",
                schema: "core",
                table: "UserRefreshTokens",
                type: "bit",
                nullable: false,
                comment: "標記該用戶所有/特定 Session 是否被安全鎖定",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDead",
                schema: "core",
                table: "UserRefreshTokens",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "核心資安防禦熔斷標記：標記該憑證是否已被重放攻擊戳死、或已被主動註銷失效",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAt",
                schema: "core",
                table: "UserRefreshTokens",
                type: "datetime2",
                nullable: false,
                comment: "憑證絕對過期時間",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceName",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                comment: "裝置名稱或瀏覽器特徵描述 (例如: Chrome on Windows)",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                comment: "裝置唯一指紋 (由前端 Vue / Blazor 經由 JS 產生的不重複雜湊值)",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "core",
                table: "UserRefreshTokens",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                comment: "建立時間",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<string>(
                name: "ClientIp",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "登入時的來源 IP 紀錄",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "core",
                table: "UserRefreshTokens",
                type: "bigint",
                nullable: false,
                comment: "識別鍵",
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserLabPermissions",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "UserLabPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "最後修改者",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserLabPermissions",
                type: "datetimeoffset(7)",
                nullable: true,
                comment: "最後修改時間 (UTC)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "UserLabPermissions",
                type: "uniqueidentifier",
                nullable: false,
                comment: "區域實驗室識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "LabId",
                schema: "core",
                table: "UserLabPermissions",
                type: "int",
                nullable: false,
                comment: "實驗室識別鍵",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserLabPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "建立者",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserLabPermissions",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                comment: "建立時間 (UTC)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerOrModuleKey",
                schema: "core",
                table: "UserLabPermissions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                comment: "控制器或模組識別鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<long>(
                name: "Bitmask",
                schema: "core",
                table: "UserLabPermissions",
                type: "bigint",
                nullable: false,
                comment: "64 位元權限遮罩值 (Bitmask 0-63)",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "UserLabPermissions",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: true,
                comment: "最後更新時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "UserLabMappings",
                type: "uniqueidentifier",
                nullable: false,
                comment: "區域實驗室識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "JobTitle",
                schema: "core",
                table: "UserLabMappings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "職位標題",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPrimary",
                schema: "core",
                table: "UserLabMappings",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否為主要實驗室",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "UserLabMappings",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "是否啟用",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiryDate",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: true,
                comment: "失效日期",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveDate",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: false,
                comment: "生效日期",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserLabMappings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "建立人員識別鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<int>(
                name: "LabId",
                schema: "core",
                table: "UserLabMappings",
                type: "int",
                nullable: false,
                comment: "實驗室識別鍵",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserLabMappings",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                comment: "權限識別鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<long>(
                name: "Bitmask",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "bigint",
                nullable: false,
                comment: "64 位元權限遮罩值 (Bitmask 0-63)",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                schema: "core",
                table: "SystemSettings",
                type: "nvarchar(max)",
                nullable: false,
                comment: "設定內容（JSON）",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "UpdatedByUserId",
                schema: "core",
                table: "SystemSettings",
                type: "uniqueidentifier",
                nullable: true,
                comment: "更新者用戶識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedByName",
                schema: "core",
                table: "SystemSettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "更新者名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "core",
                table: "SystemSettings",
                type: "datetimeoffset",
                nullable: false,
                comment: "更新時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                schema: "core",
                table: "SystemSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "異動摘要",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "SettingKey",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "設定分類鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "ClientIp",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "客戶端 IP",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "uniqueidentifier",
                nullable: true,
                comment: "操作者用戶識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActorName",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "操作者名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "用戶識別碼",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(2083)",
                maxLength: 2083,
                nullable: true,
                comment: "URL",
                oldClrType: typeof(string),
                oldType: "nvarchar(2083)",
                oldMaxLength: 2083,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "TimeStamp",
                schema: "core",
                table: "SystemLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                comment: "時間戳記",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "區域實驗室關聯識別碼",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrevHash",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "前一筆雜湊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "有效載荷",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Operation",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "操作",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "模組名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "訊息",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "層級",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IP",
                schema: "core",
                table: "SystemLogs",
                type: "varchar(45)",
                unicode: false,
                maxLength: 45,
                nullable: true,
                comment: "IP位址",
                oldClrType: typeof(string),
                oldType: "varchar(45)",
                oldUnicode: false,
                oldMaxLength: 45,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Fingerprint",
                schema: "core",
                table: "SystemLogs",
                type: "char(64)",
                nullable: true,
                comment: "特徵碼",
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Exception",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "異常",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrentHash",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "雜湊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "SystemLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                schema: "core",
                table: "SystemLogs",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                comment: "關聯識別碼",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AlertId",
                schema: "core",
                table: "SystemLogs",
                type: "char(32)",
                nullable: true,
                comment: "告警識別碼",
                oldClrType: typeof(string),
                oldType: "char(32)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "core",
                table: "SystemLogs",
                type: "bigint",
                nullable: false,
                comment: "識別鍵",
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(128)",
                maxLength: 128,
                nullable: true,
                comment: "觸發事件的用戶識別碼",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "core",
                table: "SecurityLog",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                comment: "高精度時間戳記 (帶時區)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'");

            migrationBuilder.AlterColumn<string>(
                name: "Properties",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(MAX)",
                nullable: true,
                comment: "隨附結構化快照內容 (JSON 格式)",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(MAX)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(MAX)",
                nullable: true,
                comment: "日誌訊息",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(MAX)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LogType",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(64)",
                maxLength: 64,
                nullable: true,
                comment: "日誌分流類型 (例如: Security)",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(128)",
                maxLength: 128,
                nullable: true,
                comment: "日誌層級 (例如: Information, Warning, Error)",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Fingerprint",
                schema: "core",
                table: "SecurityLog",
                type: "char(64)",
                nullable: true,
                comment: "特徵碼",
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Exception",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(MAX)",
                nullable: true,
                comment: "異常堆疊追蹤資訊",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(MAX)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventCategory",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(128)",
                maxLength: 128,
                nullable: true,
                comment: "稽核事件分類 (例如: Auth.TokenRefresh)",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                schema: "core",
                table: "SecurityLog",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                comment: "關聯識別碼",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientIp",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(64)",
                maxLength: 64,
                nullable: true,
                comment: "客戶端來源 IP 位址",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AlertId",
                schema: "core",
                table: "SecurityLog",
                type: "char(32)",
                nullable: true,
                comment: "告警識別碼",
                oldClrType: typeof(string),
                oldType: "char(32)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "core",
                table: "SecurityLog",
                type: "int",
                nullable: false,
                comment: "流水號主鍵",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "RoleLabScopes",
                type: "uniqueidentifier",
                nullable: false,
                comment: "區域實驗室的 TenantLabId（ORG Organization.TenantLabId）",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "RoleLabScopes",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "TargetPermissionKeys",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(max)",
                nullable: false,
                comment: "核准後要套用的完整權限鍵清單（JSON 陣列）",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(450)",
                nullable: false,
                comment: "申請狀態",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "角色名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "RoleChangeRequests",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ReviewedByName",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "審核者名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReviewedBy",
                schema: "core",
                table: "RoleChangeRequests",
                type: "uniqueidentifier",
                nullable: true,
                comment: "審核者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReviewedAt",
                schema: "core",
                table: "RoleChangeRequests",
                type: "datetimeoffset",
                nullable: true,
                comment: "審核時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReviewComment",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "審核意見",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestedByName",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "申請者名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedBy",
                schema: "core",
                table: "RoleChangeRequests",
                type: "uniqueidentifier",
                nullable: false,
                comment: "申請者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RequestedAt",
                schema: "core",
                table: "RoleChangeRequests",
                type: "datetimeoffset",
                nullable: false,
                comment: "申請時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "RequestReason",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "提出者說明（變更原因）",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RemovedPermissionKeys",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(max)",
                nullable: false,
                comment: "相對於送出當下的移除權限（JSON 陣列）",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PreviousRoleStatus",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(max)",
                nullable: true,
                comment: "送出申請當下的角色狀態",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AffectedUserCount",
                schema: "core",
                table: "RoleChangeRequests",
                type: "int",
                nullable: false,
                comment: "送出當下的角色成員數（影響範圍）",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "AddedPermissionKeys",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(max)",
                nullable: false,
                comment: "相對於送出當下的新增權限（JSON 陣列）",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "RoleChangeRequests",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                comment: "人可讀的異動摘要",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "異動當下的角色名稱（角色刪除或更名後仍可辨識）",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "RoleAuditLogs",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Detail",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "補充明細（JSON，例如權限差異、成員清單）",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "RoleAuditLogs",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<int>(
                name: "AffectedUserCount",
                schema: "core",
                table: "RoleAuditLogs",
                type: "int",
                nullable: false,
                comment: "受影響的使用者人數",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                schema: "core",
                table: "RoleAuditLogs",
                type: "uniqueidentifier",
                nullable: true,
                comment: "操作者用戶識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActorName",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "操作者名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "動作代碼，見 RoleAuditActions",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "RoleAuditLogs",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "updated_by",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "varchar(256)",
                unicode: false,
                maxLength: 256,
                nullable: true,
                comment: "最後修改者",
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldUnicode: false,
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at_utc",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "datetimeoffset",
                nullable: true,
                comment: "最後修改時間 (UTC)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "datetime2",
                nullable: true,
                comment: "最後修改時間",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "role_id",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "varchar(450)",
                unicode: false,
                maxLength: 450,
                nullable: false,
                comment: "角色識別碼 (對應 ASP.NET Core Identity RoleId)",
                oldClrType: typeof(string),
                oldType: "varchar(450)",
                oldUnicode: false,
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "permission_key",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "varchar(150)",
                unicode: false,
                maxLength: 150,
                nullable: false,
                comment: "全域權限或模組識別 Key",
                oldClrType: typeof(string),
                oldType: "varchar(150)",
                oldUnicode: false,
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "varchar(256)",
                unicode: false,
                maxLength: 256,
                nullable: false,
                comment: "建立者",
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldUnicode: false,
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at_utc",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間 (UTC)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "datetime2",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<long>(
                name: "bitmask",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "bigint",
                nullable: false,
                comment: "64 位元權限遮罩值 (Bitmask 0-63)",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "uniqueidentifier",
                nullable: false,
                comment: "唯一識別碼 (Primary Key)",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "RemediationTickets",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "關聯的用戶識別碼",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "RemediationReason",
                schema: "core",
                table: "RemediationTickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "核發此憑證時的資安風險原因說明",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "IssuedAt",
                schema: "core",
                table: "RemediationTickets",
                type: "datetime2",
                nullable: false,
                comment: "憑證核發時間",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                schema: "core",
                table: "RemediationTickets",
                type: "bit",
                nullable: false,
                comment: "該憑證是否已被使用：關鍵管控，防止 Replay Attack（重放攻擊）",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAt",
                schema: "core",
                table: "RemediationTickets",
                type: "datetime2",
                nullable: false,
                comment: "憑證失效截止時間：關鍵管控，時效性驗證，逾期即作廢",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByIp",
                schema: "core",
                table: "RemediationTickets",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "核發此憑證時的客戶端來源 IP (供後續日誌鑑識比對)",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "TicketId",
                schema: "core",
                table: "RemediationTickets",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "憑證唯一識別碼 (通常為安全隨機生成的 GUID 或加密記號)",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "PermissionGrants",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<byte[]>(
                name: "PermissionVector",
                schema: "core",
                table: "PermissionGrants",
                type: "varbinary(64)",
                nullable: false,
                comment: "動態 BitMask 權限位元組陣列 (預設 64 bytes = 512 個權限點)",
                oldClrType: typeof(byte[]),
                oldType: "varbinary(64)");

            migrationBuilder.AlterColumn<Guid>(
                name: "LabId",
                schema: "core",
                table: "PermissionGrants",
                type: "uniqueidentifier",
                nullable: false,
                comment: "跨實驗室隔離維度 ID",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "PermissionGrants",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Version",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "1.0.0",
                comment: "版本",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "1.0.0");

            migrationBuilder.AlterColumn<string>(
                name: "ModuleTitle",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "模組標題",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "模組名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastLoadedAt",
                schema: "core",
                table: "ModuleMetadatas",
                type: "datetime2",
                nullable: false,
                comment: "最後載入時間",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "ModuleMetadatas",
                type: "bit",
                nullable: false,
                comment: "是否啟用",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Checksum",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "校驗和",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "AssemblyPath",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                comment: "組件路徑",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ModuleMetadatas",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                comment: "節點類型 (Section, Group, Page)",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Route",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "前端路由路徑 (僅 MenuType.Page 必須填寫)",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "關聯的權限鍵值",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentId",
                schema: "core",
                table: "MenuItems",
                type: "uniqueidentifier",
                nullable: true,
                comment: "父節點識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "模組識別碼 (區分主專案與 SES 外掛模組，如: \"System\", \"ORG\")",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "節點識別 Key (例如: ORG.LABORATORY.LIST)",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<bool>(
                name: "IsVisible",
                schema: "core",
                table: "MenuItems",
                type: "bit",
                nullable: false,
                comment: "是否在選單中顯示 (預留隱藏路由支援)",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "MenuItems",
                type: "bit",
                nullable: false,
                comment: "是否啟用",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(max)",
                nullable: true,
                comment: "圖示",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DisplayOrder",
                schema: "core",
                table: "MenuItems",
                type: "int",
                nullable: false,
                comment: "顯示排序",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "顯示名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "ControllerId",
                schema: "core",
                table: "MenuItems",
                type: "uniqueidentifier",
                nullable: true,
                comment: "控制器識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Component",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "MenuItems",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedByName",
                schema: "core",
                table: "LabManagers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "最後更新人員名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "core",
                table: "LabManagers",
                type: "datetimeoffset",
                nullable: false,
                comment: "最後更新時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "ManagerUserId",
                schema: "core",
                table: "LabManagers",
                type: "uniqueidentifier",
                nullable: false,
                comment: "主管使用者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "LabManagers",
                type: "uniqueidentifier",
                nullable: false,
                comment: "區域實驗室識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "LabFunctionScopes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "權限鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "LabFunctionScopes",
                type: "uniqueidentifier",
                nullable: false,
                comment: "區域實驗室識別碼（ORG Organization.TenantLabId）",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedByName",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "最後更新人員名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UpdatedBy",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "uniqueidentifier",
                nullable: true,
                comment: "最後更新人員識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "datetimeoffset",
                nullable: false,
                comment: "最後更新時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRestricted",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "bit",
                nullable: false,
                comment: "是否限制功能",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "uniqueidentifier",
                nullable: false,
                comment: "區域實驗室識別碼（ORG Organization.TenantLabId）",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "LabAuditAssignments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "LabAuditAssignments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "受稽核的區域實驗室（ORG Organization.TenantLabId）",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "RevokedByName",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "撤銷人員識別鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RevokedAt",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: true,
                comment: "撤銷時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RevokeReason",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "撤銷原因",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                comment: "稽核原因 / 稽核案號",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveTo",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: false,
                comment: "生效迄止（含）",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveFrom",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: false,
                comment: "生效起始（含）",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: false,
                comment: "指派時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedByName",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "指派人員名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssignedBy",
                schema: "core",
                table: "LabAuditAssignments",
                type: "uniqueidentifier",
                nullable: true,
                comment: "指派人員識別鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "LabAuditAssignments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "內部變更原因（僅管理者與稽核可見）",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "權限金鑰",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Notice",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "對使用者公告內容（停用時顯示於系統公告與 API 拒絕訊息）",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "bit",
                nullable: false,
                comment: "異動後狀態：true = 啟用、false = 停用",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpectedRestoreAt",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "datetimeoffset",
                nullable: true,
                comment: "預計恢復時間（僅供公告，不會自動恢復）",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "功能顯示名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "異動當下的功能識別（中繼資料重新掃描或移除後仍可辨識）",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "ControllerMetadataId",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "uniqueidentifier",
                nullable: false,
                comment: "控制器中繼資料識別",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ClientIp",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "來源 IP",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "uniqueidentifier",
                nullable: true,
                comment: "操作者識別",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActorName",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "操作者名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActionName",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "動作名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ControllerMetadatas",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "使用者識別",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                schema: "core",
                table: "AuditLogs",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                comment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)",
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldUnicode: false,
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "core",
                table: "AuditLogs",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                comment: "時間戳記",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "資料表名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "StoredHash",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "儲存雜湊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Schema",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "資料表綱要",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RepairedAt",
                schema: "core",
                table: "AuditLogs",
                type: "datetimeoffset(7)",
                nullable: true,
                comment: "修復時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RemoteIp",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "遠端 IP 位址",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreviousHash",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "前一筆雜湊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalStoredHash",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "原始儲存雜湊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OldValues",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "舊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NewValues",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "新值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "KeyValues",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "主鍵值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRepaired",
                schema: "core",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否已修復",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "GapReason",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "差異原因",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChangedColumns",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "變更欄位",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "操作類型",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "AuditLogs",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "core",
                table: "AuditLogs",
                type: "bigint",
                nullable: false,
                comment: "稽核日誌識別主鍵",
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                comment: "帳號目前狀態",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastLoginAt",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true,
                comment: "帳號最後登入時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystemAdmin",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                comment: "是否為系統管理員",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                comment: "帳號建立者",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                comment: "使用者真實姓名",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeNo",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                comment: "員工編號",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedBy",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                comment: "帳號刪除者",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true,
                comment: "帳號刪除時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                comment: "帳號建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "AspNetRoles",
                type: "uniqueidentifier",
                nullable: true,
                comment: "區域實驗室專屬角色的所屬租戶實驗室 ID（全域或系統角色則為 Null）",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                comment: "角色狀態(0 = 停用, 1 = 啟用中,2=已鎖定 ,3=待審核)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ScopeType",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "LabScoped",
                comment: "角色範圍類型 (SystemDefault, Global, LabScoped)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "LabScoped");

            migrationBuilder.AlterColumn<bool>(
                name: "RequiresAuditAssignment",
                schema: "core",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否為稽核角色。預設為 false；僅在使用者具有效「稽核指派」的區域實驗室中生效。",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystemRole",
                schema: "core",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否為系統內建角色。預設為 false；系統初始化範本會明確設為 true。",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "core",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                comment: "是否已刪除",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<int>(
                name: "HierarchyLevel",
                schema: "core",
                table: "AspNetRoles",
                type: "int",
                nullable: false,
                defaultValue: 10,
                comment: "角色管理階層數值（數字越大位階越高。例: SA=100, MO=50, MA=10）",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 10);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveTo",
                schema: "core",
                table: "AspNetRoles",
                type: "datetimeoffset",
                nullable: true,
                comment: "生效迄日（含）；null 表示無期限",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveFrom",
                schema: "core",
                table: "AspNetRoles",
                type: "datetimeoffset",
                nullable: true,
                comment: "生效起日（含）；null 表示立即生效。生效期間外角色不授予任何權限",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                comment: "角色描述",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "角色代碼",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "使用者帳號",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: false,
                comment: "覆核活動名稱",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "角色名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryLabName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "快照：使用者主要實驗室名稱（覆核者依此範圍覆核所屬人員）",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PrimaryLabId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: true,
                comment: "快照：使用者主要實驗室（覆核者依此範圍覆核所屬人員）",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "使用者全名",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Decision",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(450)",
                nullable: false,
                comment: "覆核決定",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "DecidedByName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "覆核決定人員名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DecidedBy",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: true,
                comment: "覆核決定時間",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DecidedAt",
                schema: "core",
                table: "AccessReviewItems",
                type: "datetimeoffset",
                nullable: true,
                comment: "覆核決定時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "覆核決定說明",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: false,
                comment: "覆核活動識別",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(max)",
                nullable: true,
                comment: "使用者電子郵件");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(450)",
                nullable: false,
                comment: "活動狀態",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "RoleScope",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(max)",
                nullable: false,
                comment: "覆核範圍的角色 ID（JSON 陣列）；空陣列表示全部角色",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                comment: "覆核活動名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DueDate",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "datetimeoffset",
                nullable: false,
                comment: "覆核期限",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "覆核活動描述",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "覆核範圍的實驗室 ID",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "ClosedBy",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "覆核結束人員",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ClosedAt",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "datetimeoffset",
                nullable: true,
                comment: "覆核結束時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_UserId",
                schema: "core",
                table: "SystemLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SystemLogs_UserId",
                schema: "core",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "core",
                table: "AccessReviewItems");

            migrationBuilder.AlterTable(
                name: "UserRoleExpirations",
                schema: "core",
                oldComment: "過渡期角色（Transition Role）：角色指派的有效期限。調職交接時保留舊角色一段時間，到期由排程自動回收，避免權限累積 (Privilege Creep)。每位使用者在同一角色上最多一筆未處理的期限。");

            migrationBuilder.AlterTable(
                name: "UserResourceGrants",
                schema: "core",
                oldComment: "使用者資源授權實體，表示特定使用者對特定資源（如功能表項目）的存取權限");

            migrationBuilder.AlterTable(
                name: "UserLabPermissions",
                schema: "core",
                oldComment: "使用者實驗室專屬 64 位元遮罩權限實體 (對齊 UserLabMapping 型別與稽核標準)");

            migrationBuilder.AlterTable(
                name: "UserLabMappings",
                schema: "core",
                oldComment: "用戶與實驗室映射（管理用戶與實驗室之間的關聯，包括用戶的主要實驗室和職位標題等信息）");

            migrationBuilder.AlterTable(
                name: "UserGlobalPermissions",
                schema: "core",
                oldComment: "使用者全域/組織級 64 位元遮罩權限實體 (統一 UserId 為 Guid 型別)");

            migrationBuilder.AlterTable(
                name: "SystemSettings",
                schema: "core",
                oldComment: "系統配置（管理者可於執行期調整的設定，例如外觀識別、密碼規則）。以「分類鍵 + JSON 值」儲存，新增設定分類時不需異動資料表結構。");

            migrationBuilder.AlterTable(
                name: "SystemSettingChangeLogs",
                schema: "core",
                oldComment: "系統配置異動歷程（稽核用，僅新增不修改）");

            migrationBuilder.AlterTable(
                name: "SystemLogs",
                schema: "core",
                oldComment: "系統日誌實體：用於記錄系統運行狀態、事件、錯誤與稽核資訊")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true)
                .OldAnnotation("SqlServer:IsLedger", true)
                .OldAnnotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.AlterTable(
                name: "SecurityLog",
                schema: "core",
                oldComment: "安全性審計總帳實體模型 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true)
                .OldAnnotation("SqlServer:IsLedger", true)
                .OldAnnotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.AlterTable(
                name: "RoleLabScopes",
                schema: "core",
                oldComment: "實驗室專屬角色適用的區域實驗室（一個角色可指派多個實驗室）");

            migrationBuilder.AlterTable(
                name: "RoleChangeRequests",
                schema: "core",
                oldComment: "角色權限變更申請，用於管理角色權限的變更流程。");

            migrationBuilder.AlterTable(
                name: "RoleAuditLogs",
                schema: "core",
                oldComment: "角色異動歷程（稽核用，僅新增不修改）。記錄角色狀態、設定、權限、成員、AD 群組等所有異動，含操作者、時間與影響人數。");

            migrationBuilder.AlterTable(
                name: "Role_Global_Permissions",
                schema: "core",
                oldComment: "角色全域/模組級 64 位元遮罩權限實體");

            migrationBuilder.AlterTable(
                name: "RemediationTickets",
                schema: "core",
                oldComment: "身分補償修復憑證實體：專門用於記錄因資安熔斷（Lockdown）後，允許用戶執行高風險密碼修復或身分重驗的臨時安全 Ticket。");

            migrationBuilder.AlterTable(
                name: "PermissionGrants",
                schema: "core",
                oldComment: "角色權限授權實體，表示特定角色對特定功能或資源的存取權限。");

            migrationBuilder.AlterTable(
                name: "ModuleMetadatas",
                schema: "core",
                oldComment: "模組元資料實體，包含模組的基本資訊與狀態");

            migrationBuilder.AlterTable(
                name: "MenuItems",
                schema: "core",
                oldComment: "動態選單實體 (Domain Entity)");

            migrationBuilder.AlterTable(
                name: "LabManagers",
                schema: "core",
                oldComment: "區域實驗室主管（以 TenantLabId 對應 ORG 區域實驗室，與 UserLabMapping 相同的鍵值維度）");

            migrationBuilder.AlterTable(
                name: "LabFunctionScopes",
                schema: "core",
                oldComment: "區域實驗室開放的權限（完整權限鍵，例：ORG.LABORATORY.READ）");

            migrationBuilder.AlterTable(
                name: "LabFunctionPolicies",
                schema: "core",
                oldComment: "區域實驗室的功能開放政策。IsRestricted = false（或無資料）表示所有功能開放；IsRestricted = true 時，僅 LabFunctionScope 列出的權限於該實驗室生效（對所有使用者與角色的上限，系統管理員除外）。");

            migrationBuilder.AlterTable(
                name: "LabAuditAssignments",
                schema: "core",
                oldComment: "實驗室稽核指派：指定使用者於起訖期間內對某區域實驗室執行稽核職責。有效期間內，使用者所持有的稽核角色（ApplicationRole.RequiresAuditAssignment）權限於該實驗室生效，且該實驗室會出現在使用者可切換的實驗室清單中；期間外或撤銷後自動失效。紀錄不刪除，保留完整稽核軌跡。");

            migrationBuilder.AlterTable(
                name: "FunctionStatusLogs",
                schema: "core",
                oldComment: "系統功能啟用 / 停用異動歷程（稽核用，僅新增不修改）");

            migrationBuilder.AlterTable(
                name: "AuditLogs",
                schema: "core",
                oldComment: "資料稽核日誌實體");

            migrationBuilder.AlterTable(
                name: "AspNetUsers",
                schema: "core",
                oldComment: "擴充自訂的 Identity 使用者實體");

            migrationBuilder.AlterTable(
                name: "AspNetRoles",
                schema: "core",
                oldComment: "企業級自訂應用程式角色實體 (支援 ARBAC 與多實驗室隔離)");

            migrationBuilder.AlterTable(
                name: "AccessReviewItems",
                schema: "core",
                oldComment: "覆核項目：一位使用者在一個角色上的指派");

            migrationBuilder.AlterTable(
                name: "AccessReviewCampaigns",
                schema: "core",
                oldComment: "定期權限覆核活動 (Access Certification Campaign)。建立時快照當下所有（或指定角色的）成員指派，由覆核者逐一確認「保留」或「回收」。");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserRoleExpirations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "UserRoleExpirations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "core",
                table: "UserRoleExpirations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "設定原因，例如「調任銷售部，業務交接」");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "core",
                table: "UserRoleExpirations",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "到期時間（UTC），到期後由排程自動解除角色指派");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserRoleExpirations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "建立者");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "UserRoleExpirations",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "UserRoleExpirations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ValidTo",
                schema: "core",
                table: "UserResourceGrants",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "有效結束時間");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ValidFrom",
                schema: "core",
                table: "UserResourceGrants",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "有效起始時間");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionType",
                schema: "core",
                table: "UserResourceGrants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldComment: "權限類型");

            migrationBuilder.AlterColumn<Guid>(
                name: "MenuItemId",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "功能表項目識別鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "LabId",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "實驗室識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "EffectType",
                schema: "core",
                table: "UserResourceGrants",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldComment: "權限效果");

            migrationBuilder.AlterColumn<Guid>(
                name: "DelegatorUserId",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "被代理人識別鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "UserResourceGrants",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "使用者唯一識別碼");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RotatedAt",
                schema: "core",
                table: "UserRefreshTokens",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldComment: "上一次成功輪換的時間（用於寬限期計算）");

            migrationBuilder.AlterColumn<string>(
                name: "RiskReason",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "記錄風險觸發原因或主動登出/熔斷稽核事由");

            migrationBuilder.AlterColumn<string>(
                name: "ReusedTokenCache",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "快取上一次生成的全新 TokenHash，供寬限期內的併發請求直接複用");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastActiveAt",
                schema: "core",
                table: "UserRefreshTokens",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldComment: "該裝置最後一次發起請求或換票的活動時間 (用於 LIFO 被動擠出策略排序)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsFrozen",
                schema: "core",
                table: "UserRefreshTokens",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "標記該用戶所有/特定 Session 是否被安全鎖定");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDead",
                schema: "core",
                table: "UserRefreshTokens",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false,
                oldComment: "核心資安防禦熔斷標記：標記該憑證是否已被重放攻擊戳死、或已被主動註銷失效");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAt",
                schema: "core",
                table: "UserRefreshTokens",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldComment: "憑證絕對過期時間");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceName",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldComment: "裝置名稱或瀏覽器特徵描述 (例如: Chrome on Windows)");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldComment: "裝置唯一指紋 (由前端 Vue / Blazor 經由 JS 產生的不重複雜湊值)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "core",
                table: "UserRefreshTokens",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<string>(
                name: "ClientIp",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldComment: "登入時的來源 IP 紀錄");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "core",
                table: "UserRefreshTokens",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "識別鍵")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserLabPermissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "UserLabPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "最後修改者");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserLabPermissions",
                type: "datetimeoffset(7)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true,
                oldComment: "最後修改時間 (UTC)");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "UserLabPermissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "區域實驗室識別鍵");

            migrationBuilder.AlterColumn<int>(
                name: "LabId",
                schema: "core",
                table: "UserLabPermissions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "實驗室識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserLabPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "建立者");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserLabPermissions",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()",
                oldComment: "建立時間 (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerOrModuleKey",
                schema: "core",
                table: "UserLabPermissions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldComment: "控制器或模組識別鍵");

            migrationBuilder.AlterColumn<long>(
                name: "Bitmask",
                schema: "core",
                table: "UserLabPermissions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "64 位元權限遮罩值 (Bitmask 0-63)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "UserLabPermissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true,
                oldComment: "最後更新時間");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "UserLabMappings",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "區域實驗室識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "JobTitle",
                schema: "core",
                table: "UserLabMappings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "職位標題");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPrimary",
                schema: "core",
                table: "UserLabMappings",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false,
                oldComment: "是否為主要實驗室");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "UserLabMappings",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true,
                oldComment: "是否啟用");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiryDate",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true,
                oldComment: "失效日期");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveDate",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldComment: "生效日期");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserLabMappings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "建立人員識別鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<int>(
                name: "LabId",
                schema: "core",
                table: "UserLabMappings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "實驗室識別鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserLabMappings",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldComment: "權限識別鍵");

            migrationBuilder.AlterColumn<long>(
                name: "Bitmask",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "64 位元權限遮罩值 (Bitmask 0-63)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                schema: "core",
                table: "SystemSettings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "設定內容（JSON）");

            migrationBuilder.AlterColumn<Guid>(
                name: "UpdatedByUserId",
                schema: "core",
                table: "SystemSettings",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "更新者用戶識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedByName",
                schema: "core",
                table: "SystemSettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "更新者名稱");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "core",
                table: "SystemSettings",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "更新時間");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                schema: "core",
                table: "SystemSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldComment: "異動摘要");

            migrationBuilder.AlterColumn<string>(
                name: "SettingKey",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "設定分類鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<string>(
                name: "ClientIp",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "客戶端 IP");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "操作者用戶識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "ActorName",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "操作者名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "SystemSettingChangeLogs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "用戶識別碼");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(2083)",
                maxLength: 2083,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2083)",
                oldMaxLength: 2083,
                oldNullable: true,
                oldComment: "URL");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "TimeStamp",
                schema: "core",
                table: "SystemLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldComment: "時間戳記");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "區域實驗室關聯識別碼");

            migrationBuilder.AlterColumn<string>(
                name: "PrevHash",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "前一筆雜湊值");

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "有效載荷");

            migrationBuilder.AlterColumn<string>(
                name: "Operation",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "操作");

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "模組名稱");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "訊息");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "層級");

            migrationBuilder.AlterColumn<string>(
                name: "IP",
                schema: "core",
                table: "SystemLogs",
                type: "varchar(45)",
                unicode: false,
                maxLength: 45,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(45)",
                oldUnicode: false,
                oldMaxLength: 45,
                oldNullable: true,
                oldComment: "IP位址");

            migrationBuilder.AlterColumn<string>(
                name: "Fingerprint",
                schema: "core",
                table: "SystemLogs",
                type: "char(64)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldNullable: true,
                oldComment: "特徵碼");

            migrationBuilder.AlterColumn<string>(
                name: "Exception",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "異常");

            migrationBuilder.AlterColumn<string>(
                name: "CurrentHash",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "雜湊值");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "SystemLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                schema: "core",
                table: "SystemLogs",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "關聯識別碼");

            migrationBuilder.AlterColumn<string>(
                name: "AlertId",
                schema: "core",
                table: "SystemLogs",
                type: "char(32)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(32)",
                oldNullable: true,
                oldComment: "告警識別碼");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "core",
                table: "SystemLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "識別鍵")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "觸發事件的用戶識別碼");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "core",
                table: "SecurityLog",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldComment: "高精度時間戳記 (帶時區)");

            migrationBuilder.AlterColumn<string>(
                name: "Properties",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(MAX)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(MAX)",
                oldNullable: true,
                oldComment: "隨附結構化快照內容 (JSON 格式)");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(MAX)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(MAX)",
                oldNullable: true,
                oldComment: "日誌訊息");

            migrationBuilder.AlterColumn<string>(
                name: "LogType",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "日誌分流類型 (例如: Security)");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "日誌層級 (例如: Information, Warning, Error)");

            migrationBuilder.AlterColumn<string>(
                name: "Fingerprint",
                schema: "core",
                table: "SecurityLog",
                type: "char(64)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldNullable: true,
                oldComment: "特徵碼");

            migrationBuilder.AlterColumn<string>(
                name: "Exception",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(MAX)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(MAX)",
                oldNullable: true,
                oldComment: "異常堆疊追蹤資訊");

            migrationBuilder.AlterColumn<string>(
                name: "EventCategory",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "稽核事件分類 (例如: Auth.TokenRefresh)");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                schema: "core",
                table: "SecurityLog",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "關聯識別碼");

            migrationBuilder.AlterColumn<string>(
                name: "ClientIp",
                schema: "core",
                table: "SecurityLog",
                type: "NVARCHAR(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "客戶端來源 IP 位址");

            migrationBuilder.AlterColumn<string>(
                name: "AlertId",
                schema: "core",
                table: "SecurityLog",
                type: "char(32)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(32)",
                oldNullable: true,
                oldComment: "告警識別碼");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "core",
                table: "SecurityLog",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "流水號主鍵")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "RoleLabScopes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "區域實驗室的 TenantLabId（ORG Organization.TenantLabId）");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "RoleLabScopes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "TargetPermissionKeys",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "核准後要套用的完整權限鍵清單（JSON 陣列）");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "申請狀態");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "角色名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "RoleChangeRequests",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "ReviewedByName",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "審核者名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReviewedBy",
                schema: "core",
                table: "RoleChangeRequests",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "審核者識別鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReviewedAt",
                schema: "core",
                table: "RoleChangeRequests",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "審核時間");

            migrationBuilder.AlterColumn<string>(
                name: "ReviewComment",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true,
                oldComment: "審核意見");

            migrationBuilder.AlterColumn<string>(
                name: "RequestedByName",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "申請者名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedBy",
                schema: "core",
                table: "RoleChangeRequests",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "申請者識別鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RequestedAt",
                schema: "core",
                table: "RoleChangeRequests",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "申請時間");

            migrationBuilder.AlterColumn<string>(
                name: "RequestReason",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true,
                oldComment: "提出者說明（變更原因）");

            migrationBuilder.AlterColumn<string>(
                name: "RemovedPermissionKeys",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "相對於送出當下的移除權限（JSON 陣列）");

            migrationBuilder.AlterColumn<string>(
                name: "PreviousRoleStatus",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "送出申請當下的角色狀態");

            migrationBuilder.AlterColumn<int>(
                name: "AffectedUserCount",
                schema: "core",
                table: "RoleChangeRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "送出當下的角色成員數（影響範圍）");

            migrationBuilder.AlterColumn<string>(
                name: "AddedPermissionKeys",
                schema: "core",
                table: "RoleChangeRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "相對於送出當下的新增權限（JSON 陣列）");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "RoleChangeRequests",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldComment: "人可讀的異動摘要");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "異動當下的角色名稱（角色刪除或更名後仍可辨識）");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "RoleAuditLogs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Detail",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "補充明細（JSON，例如權限差異、成員清單）");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "RoleAuditLogs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<int>(
                name: "AffectedUserCount",
                schema: "core",
                table: "RoleAuditLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "受影響的使用者人數");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                schema: "core",
                table: "RoleAuditLogs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "操作者用戶識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "ActorName",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "操作者名稱");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "core",
                table: "RoleAuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "動作代碼，見 RoleAuditActions");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "RoleAuditLogs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "updated_by",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "varchar(256)",
                unicode: false,
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldUnicode: false,
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "最後修改者");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at_utc",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "最後修改時間 (UTC)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldComment: "最後修改時間");

            migrationBuilder.AlterColumn<string>(
                name: "role_id",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "varchar(450)",
                unicode: false,
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(450)",
                oldUnicode: false,
                oldMaxLength: 450,
                oldComment: "角色識別碼 (對應 ASP.NET Core Identity RoleId)");

            migrationBuilder.AlterColumn<string>(
                name: "permission_key",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "varchar(150)",
                unicode: false,
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(150)",
                oldUnicode: false,
                oldMaxLength: 150,
                oldComment: "全域權限或模組識別 Key");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "varchar(256)",
                unicode: false,
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldUnicode: false,
                oldMaxLength: 256,
                oldComment: "建立者");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at_utc",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間 (UTC)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<long>(
                name: "bitmask",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "64 位元權限遮罩值 (Bitmask 0-63)");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                schema: "core",
                table: "Role_Global_Permissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "唯一識別碼 (Primary Key)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "RemediationTickets",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "關聯的用戶識別碼");

            migrationBuilder.AlterColumn<string>(
                name: "RemediationReason",
                schema: "core",
                table: "RemediationTickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "核發此憑證時的資安風險原因說明");

            migrationBuilder.AlterColumn<DateTime>(
                name: "IssuedAt",
                schema: "core",
                table: "RemediationTickets",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldComment: "憑證核發時間");

            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                schema: "core",
                table: "RemediationTickets",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "該憑證是否已被使用：關鍵管控，防止 Replay Attack（重放攻擊）");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAt",
                schema: "core",
                table: "RemediationTickets",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldComment: "憑證失效截止時間：關鍵管控，時效性驗證，逾期即作廢");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByIp",
                schema: "core",
                table: "RemediationTickets",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "核發此憑證時的客戶端來源 IP (供後續日誌鑑識比對)");

            migrationBuilder.AlterColumn<string>(
                name: "TicketId",
                schema: "core",
                table: "RemediationTickets",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "憑證唯一識別碼 (通常為安全隨機生成的 GUID 或加密記號)");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "PermissionGrants",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別鍵");

            migrationBuilder.AlterColumn<byte[]>(
                name: "PermissionVector",
                schema: "core",
                table: "PermissionGrants",
                type: "varbinary(64)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(64)",
                oldComment: "動態 BitMask 權限位元組陣列 (預設 64 bytes = 512 個權限點)");

            migrationBuilder.AlterColumn<Guid>(
                name: "LabId",
                schema: "core",
                table: "PermissionGrants",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "跨實驗室隔離維度 ID");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "PermissionGrants",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Version",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "1.0.0",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "1.0.0",
                oldComment: "版本");

            migrationBuilder.AlterColumn<string>(
                name: "ModuleTitle",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "模組標題");

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "模組名稱");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastLoadedAt",
                schema: "core",
                table: "ModuleMetadatas",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldComment: "最後載入時間");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "ModuleMetadatas",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否啟用");

            migrationBuilder.AlterColumn<string>(
                name: "Checksum",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "校驗和");

            migrationBuilder.AlterColumn<string>(
                name: "AssemblyPath",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldComment: "組件路徑");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ModuleMetadatas",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldComment: "節點類型 (Section, Group, Page)");

            migrationBuilder.AlterColumn<string>(
                name: "Route",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "前端路由路徑 (僅 MenuType.Page 必須填寫)");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "關聯的權限鍵值");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentId",
                schema: "core",
                table: "MenuItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "父節點識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldComment: "模組識別碼 (區分主專案與 SES 外掛模組，如: \"System\", \"ORG\")");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "節點識別 Key (例如: ORG.LABORATORY.LIST)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsVisible",
                schema: "core",
                table: "MenuItems",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否在選單中顯示 (預留隱藏路由支援)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "MenuItems",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否啟用");

            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "圖示");

            migrationBuilder.AlterColumn<int>(
                name: "DisplayOrder",
                schema: "core",
                table: "MenuItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "顯示排序");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "顯示名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "ControllerId",
                schema: "core",
                table: "MenuItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "控制器識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Component",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "MenuItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedByName",
                schema: "core",
                table: "LabManagers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "最後更新人員名稱");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "core",
                table: "LabManagers",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "最後更新時間");

            migrationBuilder.AlterColumn<Guid>(
                name: "ManagerUserId",
                schema: "core",
                table: "LabManagers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "主管使用者識別鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "LabManagers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "區域實驗室識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "LabFunctionScopes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "權限鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "LabFunctionScopes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "區域實驗室識別碼（ORG Organization.TenantLabId）");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedByName",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "最後更新人員名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "UpdatedBy",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "最後更新人員識別鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "最後更新時間");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRestricted",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否限制功能");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "LabFunctionPolicies",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "區域實驗室識別碼（ORG Organization.TenantLabId）");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "LabAuditAssignments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "LabAuditAssignments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "受稽核的區域實驗室（ORG Organization.TenantLabId）");

            migrationBuilder.AlterColumn<string>(
                name: "RevokedByName",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "撤銷人員識別鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RevokedAt",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "撤銷時間");

            migrationBuilder.AlterColumn<string>(
                name: "RevokeReason",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "撤銷原因");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldComment: "稽核原因 / 稽核案號");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveTo",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "生效迄止（含）");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveFrom",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "生效起始（含）");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "指派時間");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedByName",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "指派人員名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssignedBy",
                schema: "core",
                table: "LabAuditAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "指派人員識別鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "LabAuditAssignments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "內部變更原因（僅管理者與稽核可見）");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "權限金鑰");

            migrationBuilder.AlterColumn<string>(
                name: "Notice",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "對使用者公告內容（停用時顯示於系統公告與 API 拒絕訊息）");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "異動後狀態：true = 啟用、false = 停用");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpectedRestoreAt",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "預計恢復時間（僅供公告，不會自動恢復）");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "功能顯示名稱");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "異動當下的功能識別（中繼資料重新掃描或移除後仍可辨識）");

            migrationBuilder.AlterColumn<Guid>(
                name: "ControllerMetadataId",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "控制器中繼資料識別");

            migrationBuilder.AlterColumn<string>(
                name: "ClientIp",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "來源 IP");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "操作者識別");

            migrationBuilder.AlterColumn<string>(
                name: "ActorName",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "操作者名稱");

            migrationBuilder.AlterColumn<string>(
                name: "ActionName",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "動作名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "FunctionStatusLogs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ControllerMetadatas",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "使用者識別");

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                schema: "core",
                table: "AuditLogs",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldUnicode: false,
                oldMaxLength: 64,
                oldComment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "core",
                table: "AuditLogs",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()",
                oldComment: "時間戳記");

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "資料表名稱");

            migrationBuilder.AlterColumn<string>(
                name: "StoredHash",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "儲存雜湊值");

            migrationBuilder.AlterColumn<string>(
                name: "Schema",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "資料表綱要");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RepairedAt",
                schema: "core",
                table: "AuditLogs",
                type: "datetimeoffset(7)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true,
                oldComment: "修復時間");

            migrationBuilder.AlterColumn<string>(
                name: "RemoteIp",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "遠端 IP 位址");

            migrationBuilder.AlterColumn<string>(
                name: "PreviousHash",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "前一筆雜湊值");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalStoredHash",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "原始儲存雜湊值");

            migrationBuilder.AlterColumn<string>(
                name: "OldValues",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "舊值");

            migrationBuilder.AlterColumn<string>(
                name: "NewValues",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "新值");

            migrationBuilder.AlterColumn<string>(
                name: "KeyValues",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "主鍵值");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRepaired",
                schema: "core",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false,
                oldComment: "是否已修復");

            migrationBuilder.AlterColumn<string>(
                name: "GapReason",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "差異原因");

            migrationBuilder.AlterColumn<string>(
                name: "ChangedColumns",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "變更欄位");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "core",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldComment: "操作類型");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "AuditLogs",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "core",
                table: "AuditLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "稽核日誌識別主鍵")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "帳號目前狀態");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastLoginAt",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "帳號最後登入時間");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystemAdmin",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否為系統管理員");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "帳號建立者");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "使用者真實姓名");

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeNo",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "員工編號");

            migrationBuilder.AlterColumn<string>(
                name: "DeletedBy",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "帳號刪除者");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "帳號刪除時間");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "帳號建立時間");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "AspNetRoles",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "區域實驗室專屬角色的所屬租戶實驗室 ID（全域或系統角色則為 Null）");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "角色狀態(0 = 停用, 1 = 啟用中,2=已鎖定 ,3=待審核)");

            migrationBuilder.AlterColumn<string>(
                name: "ScopeType",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "LabScoped",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "LabScoped",
                oldComment: "角色範圍類型 (SystemDefault, Global, LabScoped)");

            migrationBuilder.AlterColumn<bool>(
                name: "RequiresAuditAssignment",
                schema: "core",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false,
                oldComment: "是否為稽核角色。預設為 false；僅在使用者具有效「稽核指派」的區域實驗室中生效。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystemRole",
                schema: "core",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false,
                oldComment: "是否為系統內建角色。預設為 false；系統初始化範本會明確設為 true。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "core",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否已刪除");

            migrationBuilder.AlterColumn<int>(
                name: "HierarchyLevel",
                schema: "core",
                table: "AspNetRoles",
                type: "int",
                nullable: false,
                defaultValue: 10,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 10,
                oldComment: "角色管理階層數值（數字越大位階越高。例: SA=100, MO=50, MA=10）");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveTo",
                schema: "core",
                table: "AspNetRoles",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "生效迄日（含）；null 表示無期限");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveFrom",
                schema: "core",
                table: "AspNetRoles",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "生效起日（含）；null 表示立即生效。生效期間外角色不授予任何權限");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true,
                oldComment: "角色描述");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "角色代碼");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "使用者帳號");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "覆核活動名稱");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "角色名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別");

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryLabName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "快照：使用者主要實驗室名稱（覆核者依此範圍覆核所屬人員）");

            migrationBuilder.AlterColumn<Guid>(
                name: "PrimaryLabId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "快照：使用者主要實驗室（覆核者依此範圍覆核所屬人員）");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "使用者全名");

            migrationBuilder.AlterColumn<string>(
                name: "Decision",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "覆核決定");

            migrationBuilder.AlterColumn<string>(
                name: "DecidedByName",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "覆核決定人員名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "DecidedBy",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "覆核決定時間");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DecidedAt",
                schema: "core",
                table: "AccessReviewItems",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "覆核決定時間");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                schema: "core",
                table: "AccessReviewItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true,
                oldComment: "覆核決定說明");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignId",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "覆核活動識別");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "AccessReviewItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "活動狀態");

            migrationBuilder.AlterColumn<string>(
                name: "RoleScope",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "覆核範圍的角色 ID（JSON 陣列）；空陣列表示全部角色");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldComment: "覆核活動名稱");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DueDate",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "覆核期限");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true,
                oldComment: "覆核活動描述");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "覆核範圍的實驗室 ID");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<string>(
                name: "ClosedBy",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "覆核結束人員");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ClosedAt",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "覆核結束時間");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "AccessReviewCampaigns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");
        }
    }
}
