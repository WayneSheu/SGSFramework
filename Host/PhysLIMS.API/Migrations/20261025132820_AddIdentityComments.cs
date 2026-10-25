using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "UserRefreshTokens",
                schema: "core",
                comment: "儲存使用者裝置安全 Session 與長效憑證的資料表實體 (相容性與資安一體化規格)");

            migrationBuilder.AlterTable(
                name: "SodRules",
                schema: "core",
                comment: "職能分立規則 (SoD, Separation of Duties)：兩項互斥權限不得同時存在於同一角色。例如「建立付款單」與「核准付款單」。");

            migrationBuilder.AlterTable(
                name: "OutboxMessages",
                schema: "core",
                comment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.AlterTable(
                name: "AspNetUserTokens",
                schema: "core",
                comment: "使用者權杖：外部驗證提供者發給使用者的權杖 (如驗證器金鑰、復原碼)");

            migrationBuilder.AlterTable(
                name: "AspNetUserRoles",
                schema: "core",
                comment: "使用者角色指派：使用者與角色的多對多關聯");

            migrationBuilder.AlterTable(
                name: "AspNetUserLogins",
                schema: "core",
                comment: "使用者外部登入：使用者與外部驗證提供者 (如 Windows / SSO) 帳號的對應");

            migrationBuilder.AlterTable(
                name: "AspNetUserClaims",
                schema: "core",
                comment: "使用者宣告 (Claims)：附加於使用者的身分屬性，登入時寫入權杖");

            migrationBuilder.AlterTable(
                name: "AspNetRoleClaims",
                schema: "core",
                comment: "角色宣告 (Claims)：附加於角色的身分屬性，該角色成員登入時一併取得");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKeyB",
                schema: "core",
                table: "SodRules",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "權限鍵 B",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKeyA",
                schema: "core",
                table: "SodRules",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "權限鍵 A",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "core",
                table: "SodRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                comment: "規則名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "core",
                table: "SodRules",
                type: "bit",
                nullable: false,
                comment: "是否啟用",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "SodRules",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "規則說明",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "SodRules",
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
                table: "SodRules",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "SodRules",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "core",
                table: "OutboxMessages",
                type: "varchar(500)",
                unicode: false,
                maxLength: 500,
                nullable: false,
                comment: "事件的完整型別名稱 (包含 Namespace)，用於還原物件",
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldUnicode: false,
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ScheduledAtUtc",
                schema: "core",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true,
                comment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RetryCount",
                schema: "core",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "目前已重試的次數",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ProcessedOnUtc",
                schema: "core",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true,
                comment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OccurredOnUtc",
                schema: "core",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: false,
                comment: "事件發生的原始時間 (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                schema: "core",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: true,
                comment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDead",
                schema: "core",
                table: "OutboxMessages",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                schema: "core",
                table: "OutboxMessages",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true,
                comment: "關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)",
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldUnicode: false,
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "core",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: false,
                comment: "事件序列化後的 JSON 內容",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CausationId",
                schema: "core",
                table: "OutboxMessages",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true,
                comment: "誘發 ID：代表觸發此事件的具體 Command 名稱或 ID",
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldUnicode: false,
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                schema: "core",
                table: "AspNetUserTokens",
                type: "nvarchar(max)",
                nullable: true,
                comment: "權杖值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "core",
                table: "AspNetUserTokens",
                type: "nvarchar(450)",
                nullable: false,
                comment: "權杖名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "core",
                table: "AspNetUserTokens",
                type: "nvarchar(450)",
                nullable: false,
                comment: "權杖所屬驗證提供者",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AspNetUserTokens",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別 (AspNetUsers.Id)",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "登入帳號",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "TwoFactorEnabled",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                comment: "是否啟用雙因素驗證",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                comment: "安全戳記；密碼或安全性設定變更時更新，使既有登入憑證失效",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "PhoneNumberConfirmed",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                comment: "電話號碼是否已驗證",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                comment: "電話號碼",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                comment: "密碼雜湊值 (不儲存明碼)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUserName",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "正規化帳號 (大寫，供唯一性檢查與查詢比對)",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "正規化電子郵件 (大寫，供查詢比對)",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LockoutEnd",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true,
                comment: "帳號鎖定截止時間 (UTC)；null 表示未鎖定",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "LockoutEnabled",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                comment: "是否允許因登入失敗而鎖定帳號",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "EmailConfirmed",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                comment: "電子郵件是否已驗證",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "電子郵件",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyStamp",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                comment: "並行控制戳記 (樂觀鎖定)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccessFailedCount",
                schema: "core",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                comment: "連續登入失敗次數 (登入成功後歸零)",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "AspNetUserRoles",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別 (AspNetRoles.Id)",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AspNetUserRoles",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別 (AspNetUsers.Id)",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AspNetUserLogins",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別 (AspNetUsers.Id)",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderDisplayName",
                schema: "core",
                table: "AspNetUserLogins",
                type: "nvarchar(max)",
                nullable: true,
                comment: "外部提供者顯示名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderKey",
                schema: "core",
                table: "AspNetUserLogins",
                type: "nvarchar(450)",
                nullable: false,
                comment: "外部提供者中的使用者唯一識別",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "core",
                table: "AspNetUserLogins",
                type: "nvarchar(450)",
                nullable: false,
                comment: "外部驗證提供者名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AspNetUserClaims",
                type: "uniqueidentifier",
                nullable: false,
                comment: "使用者識別 (AspNetUsers.Id)",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ClaimValue",
                schema: "core",
                table: "AspNetUserClaims",
                type: "nvarchar(max)",
                nullable: true,
                comment: "宣告值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClaimType",
                schema: "core",
                table: "AspNetUserClaims",
                type: "nvarchar(max)",
                nullable: true,
                comment: "宣告類型",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "core",
                table: "AspNetUserClaims",
                type: "int",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "正規化角色名稱 (大寫，供唯一性檢查與查詢比對)",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "角色名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyStamp",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: true,
                comment: "並行控制戳記 (樂觀鎖定)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "AspNetRoles",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "AspNetRoleClaims",
                type: "uniqueidentifier",
                nullable: false,
                comment: "角色識別 (AspNetRoles.Id)",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ClaimValue",
                schema: "core",
                table: "AspNetRoleClaims",
                type: "nvarchar(max)",
                nullable: true,
                comment: "宣告值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClaimType",
                schema: "core",
                table: "AspNetRoleClaims",
                type: "nvarchar(max)",
                nullable: true,
                comment: "宣告類型",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "core",
                table: "AspNetRoleClaims",
                type: "int",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "UserRefreshTokens",
                schema: "core",
                oldComment: "儲存使用者裝置安全 Session 與長效憑證的資料表實體 (相容性與資安一體化規格)");

            migrationBuilder.AlterTable(
                name: "SodRules",
                schema: "core",
                oldComment: "職能分立規則 (SoD, Separation of Duties)：兩項互斥權限不得同時存在於同一角色。例如「建立付款單」與「核准付款單」。");

            migrationBuilder.AlterTable(
                name: "OutboxMessages",
                schema: "core",
                oldComment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.AlterTable(
                name: "AspNetUserTokens",
                schema: "core",
                oldComment: "使用者權杖：外部驗證提供者發給使用者的權杖 (如驗證器金鑰、復原碼)");

            migrationBuilder.AlterTable(
                name: "AspNetUserRoles",
                schema: "core",
                oldComment: "使用者角色指派：使用者與角色的多對多關聯");

            migrationBuilder.AlterTable(
                name: "AspNetUserLogins",
                schema: "core",
                oldComment: "使用者外部登入：使用者與外部驗證提供者 (如 Windows / SSO) 帳號的對應");

            migrationBuilder.AlterTable(
                name: "AspNetUserClaims",
                schema: "core",
                oldComment: "使用者宣告 (Claims)：附加於使用者的身分屬性，登入時寫入權杖");

            migrationBuilder.AlterTable(
                name: "AspNetRoleClaims",
                schema: "core",
                oldComment: "角色宣告 (Claims)：附加於角色的身分屬性，該角色成員登入時一併取得");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKeyB",
                schema: "core",
                table: "SodRules",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "權限鍵 B");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKeyA",
                schema: "core",
                table: "SodRules",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "權限鍵 A");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "core",
                table: "SodRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldComment: "規則名稱");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "core",
                table: "SodRules",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否啟用");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "SodRules",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true,
                oldComment: "規則說明");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "SodRules",
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
                table: "SodRules",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "SodRules",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "core",
                table: "OutboxMessages",
                type: "varchar(500)",
                unicode: false,
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldUnicode: false,
                oldMaxLength: 500,
                oldComment: "事件的完整型別名稱 (包含 Namespace)，用於還原物件");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ScheduledAtUtc",
                schema: "core",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldComment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。");

            migrationBuilder.AlterColumn<int>(
                name: "RetryCount",
                schema: "core",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0,
                oldComment: "目前已重試的次數");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ProcessedOnUtc",
                schema: "core",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldComment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。");

            migrationBuilder.AlterColumn<DateTime>(
                name: "OccurredOnUtc",
                schema: "core",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldComment: "事件發生的原始時間 (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                schema: "core",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDead",
                schema: "core",
                table: "OutboxMessages",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false,
                oldComment: "當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                schema: "core",
                table: "OutboxMessages",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldUnicode: false,
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "core",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "事件序列化後的 JSON 內容");

            migrationBuilder.AlterColumn<string>(
                name: "CausationId",
                schema: "core",
                table: "OutboxMessages",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldUnicode: false,
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "誘發 ID：代表觸發此事件的具體 Command 名稱或 ID");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                schema: "core",
                table: "AspNetUserTokens",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "權杖值");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "core",
                table: "AspNetUserTokens",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "權杖名稱");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "core",
                table: "AspNetUserTokens",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "權杖所屬驗證提供者");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AspNetUserTokens",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別 (AspNetUsers.Id)");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "登入帳號");

            migrationBuilder.AlterColumn<bool>(
                name: "TwoFactorEnabled",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否啟用雙因素驗證");

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "安全戳記；密碼或安全性設定變更時更新，使既有登入憑證失效");

            migrationBuilder.AlterColumn<bool>(
                name: "PhoneNumberConfirmed",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "電話號碼是否已驗證");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "電話號碼");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "密碼雜湊值 (不儲存明碼)");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUserName",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "正規化帳號 (大寫，供唯一性檢查與查詢比對)");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "正規化電子郵件 (大寫，供查詢比對)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LockoutEnd",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "帳號鎖定截止時間 (UTC)；null 表示未鎖定");

            migrationBuilder.AlterColumn<bool>(
                name: "LockoutEnabled",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否允許因登入失敗而鎖定帳號");

            migrationBuilder.AlterColumn<bool>(
                name: "EmailConfirmed",
                schema: "core",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "電子郵件是否已驗證");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "電子郵件");

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyStamp",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "並行控制戳記 (樂觀鎖定)");

            migrationBuilder.AlterColumn<int>(
                name: "AccessFailedCount",
                schema: "core",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "連續登入失敗次數 (登入成功後歸零)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別主鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "AspNetUserRoles",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別 (AspNetRoles.Id)");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AspNetUserRoles",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別 (AspNetUsers.Id)");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AspNetUserLogins",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別 (AspNetUsers.Id)");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderDisplayName",
                schema: "core",
                table: "AspNetUserLogins",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "外部提供者顯示名稱");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderKey",
                schema: "core",
                table: "AspNetUserLogins",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "外部提供者中的使用者唯一識別");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "core",
                table: "AspNetUserLogins",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "外部驗證提供者名稱");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "AspNetUserClaims",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "使用者識別 (AspNetUsers.Id)");

            migrationBuilder.AlterColumn<string>(
                name: "ClaimValue",
                schema: "core",
                table: "AspNetUserClaims",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "宣告值");

            migrationBuilder.AlterColumn<string>(
                name: "ClaimType",
                schema: "core",
                table: "AspNetUserClaims",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "宣告類型");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "core",
                table: "AspNetUserClaims",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "識別主鍵")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "正規化角色名稱 (大寫，供唯一性檢查與查詢比對)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "角色名稱");

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyStamp",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "並行控制戳記 (樂觀鎖定)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "AspNetRoles",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別主鍵");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                schema: "core",
                table: "AspNetRoleClaims",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "角色識別 (AspNetRoles.Id)");

            migrationBuilder.AlterColumn<string>(
                name: "ClaimValue",
                schema: "core",
                table: "AspNetRoleClaims",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "宣告值");

            migrationBuilder.AlterColumn<string>(
                name: "ClaimType",
                schema: "core",
                table: "AspNetRoleClaims",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "宣告類型");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "core",
                table: "AspNetRoleClaims",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "識別主鍵")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");
        }
    }
}
