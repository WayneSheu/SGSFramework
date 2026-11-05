using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.LOG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LOGDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "log");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "稽核日誌識別主鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "建立時間"),
                    TraceId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, comment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "使用者識別"),
                    RemoteIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "遠端 IP 位址"),
                    Timestamp = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "時間戳記"),
                    Schema = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "資料表綱要"),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "資料表名稱"),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "操作類型"),
                    KeyValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "主鍵值"),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "舊值"),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "新值"),
                    ChangedColumns = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "變更欄位"),
                    PreviousHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "前一筆雜湊值"),
                    StoredHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "儲存雜湊值"),
                    IsRepaired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "是否已修復"),
                    RepairedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true, comment: "修復時間"),
                    GapReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "差異原因"),
                    OriginalStoredHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "原始儲存雜湊值")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => new { x.Id, x.CreatedAt });
                },
                comment: "資料稽核日誌實體 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)"),
                    CausationId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "誘發 ID：代表觸發此事件的具體 Command 名稱或 ID"),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "事件的完整型別名稱 (包含 Namespace)，用於還原物件"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "事件序列化後的 JSON 內容"),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "事件發生的原始時間 (UTC)"),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。"),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。"),
                    RetryCount = table.Column<int>(type: "int", nullable: false, comment: "目前已重試的次數"),
                    IsDead = table.Column<bool>(type: "bit", nullable: false, comment: "當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。"),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                },
                comment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.CreateTable(
                name: "SecurityLog",
                schema: "log",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "流水號主鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, comment: "關聯識別碼"),
                    Message = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true, comment: "日誌訊息"),
                    Level = table.Column<string>(type: "NVARCHAR(128)", maxLength: 128, nullable: true, comment: "日誌層級 (例如: Information, Warning, Error)"),
                    Timestamp = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "高精度時間戳記 (帶時區)"),
                    Exception = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true, comment: "異常堆疊追蹤資訊"),
                    Properties = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true, comment: "隨附結構化快照內容 (JSON 格式)"),
                    LogType = table.Column<string>(type: "NVARCHAR(64)", maxLength: 64, nullable: true, comment: "日誌分流類型 (例如: Security)"),
                    EventCategory = table.Column<string>(type: "NVARCHAR(128)", maxLength: 128, nullable: true, comment: "稽核事件分類 (例如: Auth.TokenRefresh)"),
                    UserId = table.Column<string>(type: "NVARCHAR(128)", maxLength: 128, nullable: true, comment: "觸發事件的用戶識別碼"),
                    ClientIp = table.Column<string>(type: "NVARCHAR(64)", maxLength: 64, nullable: true, comment: "客戶端來源 IP 位址"),
                    AlertId = table.Column<string>(type: "char(32)", nullable: true, comment: "告警識別碼"),
                    Fingerprint = table.Column<string>(type: "char(64)", nullable: true, comment: "特徵碼")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityLog", x => x.Id);
                },
                comment: "安全性審計總帳實體模型 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.CreateTable(
                name: "SystemLogs",
                schema: "log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "識別鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeStamp = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "時間戳記"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "訊息"),
                    Level = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "層級"),
                    Exception = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "異常"),
                    TenantId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "區域實驗室關聯識別碼"),
                    TenantName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "實驗室名稱 (TenantName)"),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "用戶識別碼"),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "人員名稱"),
                    ModuleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "模組名稱"),
                    ControllerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "MVC Controller 名稱"),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "操作"),
                    OperationDesc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true, comment: "操作說明 (ControllerTitle > FunctionTitle)"),
                    CorrelationId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, comment: "關聯識別碼"),
                    IP = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true, comment: "IP位址"),
                    Url = table.Column<string>(type: "nvarchar(2083)", maxLength: 2083, nullable: true, comment: "URL"),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "有效載荷"),
                    PrevHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "前一筆雜湊值"),
                    CurrentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "雜湊值"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "建立時間"),
                    AlertId = table.Column<string>(type: "char(32)", nullable: true, comment: "告警識別碼"),
                    Fingerprint = table.Column<string>(type: "char(64)", nullable: true, comment: "特徵碼")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemLogs", x => x.Id);
                },
                comment: "系統日誌實體：用於記錄系統運行狀態、事件、錯誤與稽核資訊")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_IsRepaired",
                schema: "log",
                table: "AuditLogs",
                column: "IsRepaired",
                filter: "[IsRepaired] = 0")
                .Annotation("SqlServer:Include", new[] { "TableName", "TraceId", "GapReason" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Schema_Id",
                schema: "log",
                table: "AuditLogs",
                columns: new[] { "Schema", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TraceId_Covering",
                schema: "log",
                table: "AuditLogs",
                column: "TraceId")
                .Annotation("SqlServer:Include", new[] { "Action", "CreatedAt", "TableName" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLog_CorrelationId",
                schema: "log",
                table: "SecurityLog",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_CorrelationId",
                schema: "log",
                table: "SystemLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_TenantId",
                schema: "log",
                table: "SystemLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_TimeStamp",
                schema: "log",
                table: "SystemLogs",
                column: "TimeStamp");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_UserId",
                schema: "log",
                table: "SystemLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "log")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "log");

            migrationBuilder.DropTable(
                name: "SecurityLog",
                schema: "log")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.DropTable(
                name: "SystemLogs",
                schema: "log")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);
        }
    }
}
