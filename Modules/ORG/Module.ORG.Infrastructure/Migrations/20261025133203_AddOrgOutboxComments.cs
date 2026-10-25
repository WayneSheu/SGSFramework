using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.ORG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgOutboxComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "OutboxMessages",
                schema: "org",
                comment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.AlterTable(
                name: "Organization",
                schema: "org",
                comment: "組織/實驗室實體 (Hierarchical Entity)");

            migrationBuilder.AlterTable(
                name: "AuditLogs",
                schema: "org",
                comment: "資料稽核日誌實體");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "org",
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
                schema: "org",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true,
                comment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RetryCount",
                schema: "org",
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
                schema: "org",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true,
                comment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OccurredOnUtc",
                schema: "org",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: false,
                comment: "事件發生的原始時間 (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                schema: "org",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: true,
                comment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDead",
                schema: "org",
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
                schema: "org",
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
                schema: "org",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: false,
                comment: "事件序列化後的 JSON 內容",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CausationId",
                schema: "org",
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
                schema: "org",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                comment: "識別主鍵",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: true,
                comment: "更新者",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "org",
                table: "Organization",
                type: "datetimeoffset",
                nullable: true,
                comment: "更新時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "org",
                table: "Organization",
                type: "uniqueidentifier",
                nullable: true,
                comment: "僅 Level 2 區域實驗室實體可寫入 DB 的 TenantLabId",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ParentId",
                schema: "org",
                table: "Organization",
                type: "int",
                nullable: true,
                comment: "父節點 ID",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NodePath",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: false,
                comment: "節點路徑",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "org",
                table: "Organization",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "組織/實驗室名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: true,
                comment: "組織/實驗室位置",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                schema: "org",
                table: "Organization",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "層級 (Level)",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "org",
                table: "Organization",
                type: "bit",
                nullable: false,
                comment: "是否已刪除",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "org",
                table: "Organization",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "是否啟用",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "org",
                table: "Organization",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "組織/實驗室描述",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedOnUtc",
                schema: "org",
                table: "Organization",
                type: "datetimeoffset",
                nullable: true,
                comment: "刪除時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedBy",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: true,
                comment: "刪除者",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: false,
                comment: "建立者",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "org",
                table: "Organization",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "org",
                table: "Organization",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                comment: "組織/實驗室代碼",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "org",
                table: "Organization",
                type: "int",
                nullable: false,
                comment: "組織/實驗室 ID",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "使用者識別",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                comment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "org",
                table: "AuditLogs",
                type: "datetimeoffset",
                nullable: false,
                comment: "時間戳記",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                comment: "資料表名稱",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "StoredHash",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                comment: "儲存雜湊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Schema",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "資料表綱要",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RepairedAt",
                schema: "org",
                table: "AuditLogs",
                type: "datetimeoffset",
                nullable: true,
                comment: "修復時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RemoteIp",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "遠端 IP 位址",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreviousHash",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                comment: "前一筆雜湊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalStoredHash",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "原始儲存雜湊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OldValues",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "舊值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NewValues",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "新值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "KeyValues",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "主鍵值",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRepaired",
                schema: "org",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                comment: "是否已修復",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "GapReason",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "差異原因",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "org",
                table: "AuditLogs",
                type: "datetimeoffset",
                nullable: false,
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "ChangedColumns",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "變更欄位",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                comment: "操作類型",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "org",
                table: "AuditLogs",
                type: "bigint",
                nullable: false,
                comment: "稽核日誌識別主鍵",
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "OutboxMessages",
                schema: "org",
                oldComment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.AlterTable(
                name: "Organization",
                schema: "org",
                oldComment: "組織/實驗室實體 (Hierarchical Entity)");

            migrationBuilder.AlterTable(
                name: "AuditLogs",
                schema: "org",
                oldComment: "資料稽核日誌實體");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "org",
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
                schema: "org",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldComment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。");

            migrationBuilder.AlterColumn<int>(
                name: "RetryCount",
                schema: "org",
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
                schema: "org",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldComment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。");

            migrationBuilder.AlterColumn<DateTime>(
                name: "OccurredOnUtc",
                schema: "org",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldComment: "事件發生的原始時間 (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                schema: "org",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDead",
                schema: "org",
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
                schema: "org",
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
                schema: "org",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "事件序列化後的 JSON 內容");

            migrationBuilder.AlterColumn<string>(
                name: "CausationId",
                schema: "org",
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
                schema: "org",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "識別主鍵");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "更新者");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "org",
                table: "Organization",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "更新時間");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantLabId",
                schema: "org",
                table: "Organization",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "僅 Level 2 區域實驗室實體可寫入 DB 的 TenantLabId");

            migrationBuilder.AlterColumn<int>(
                name: "ParentId",
                schema: "org",
                table: "Organization",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComment: "父節點 ID");

            migrationBuilder.AlterColumn<string>(
                name: "NodePath",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "節點路徑");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "org",
                table: "Organization",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldComment: "組織/實驗室名稱");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "組織/實驗室位置");

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                schema: "org",
                table: "Organization",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0,
                oldComment: "層級 (Level)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "org",
                table: "Organization",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否已刪除");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "org",
                table: "Organization",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true,
                oldComment: "是否啟用");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "org",
                table: "Organization",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "組織/實驗室描述");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedOnUtc",
                schema: "org",
                table: "Organization",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "刪除時間");

            migrationBuilder.AlterColumn<string>(
                name: "DeletedBy",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "刪除者");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "org",
                table: "Organization",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "建立者");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "org",
                table: "Organization",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "org",
                table: "Organization",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldComment: "組織/實驗室代碼");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "org",
                table: "Organization",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "組織/實驗室 ID")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "使用者識別");

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "org",
                table: "AuditLogs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "時間戳記");

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "資料表名稱");

            migrationBuilder.AlterColumn<string>(
                name: "StoredHash",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "儲存雜湊值");

            migrationBuilder.AlterColumn<string>(
                name: "Schema",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "資料表綱要");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RepairedAt",
                schema: "org",
                table: "AuditLogs",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "修復時間");

            migrationBuilder.AlterColumn<string>(
                name: "RemoteIp",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "遠端 IP 位址");

            migrationBuilder.AlterColumn<string>(
                name: "PreviousHash",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "前一筆雜湊值");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalStoredHash",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "原始儲存雜湊值");

            migrationBuilder.AlterColumn<string>(
                name: "OldValues",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "舊值");

            migrationBuilder.AlterColumn<string>(
                name: "NewValues",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "新值");

            migrationBuilder.AlterColumn<string>(
                name: "KeyValues",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "主鍵值");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRepaired",
                schema: "org",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否已修復");

            migrationBuilder.AlterColumn<string>(
                name: "GapReason",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "差異原因");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "org",
                table: "AuditLogs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<string>(
                name: "ChangedColumns",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "變更欄位");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "org",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "操作類型");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "org",
                table: "AuditLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "稽核日誌識別主鍵")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");
        }
    }
}
