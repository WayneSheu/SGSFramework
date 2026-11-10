using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class DropMainDbLogTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "core")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.DropTable(
                name: "SecurityLog",
                schema: "core")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.DropTable(
                name: "SystemLogs",
                schema: "core")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "稽核日誌識別主鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "建立時間"),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "操作類型"),
                    ChangedColumns = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "變更欄位"),
                    GapReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "差異原因"),
                    IsRepaired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "是否已修復"),
                    KeyValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "主鍵值"),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "新值"),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "舊值"),
                    OriginalStoredHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "原始儲存雜湊值"),
                    PreviousHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "前一筆雜湊值"),
                    RemoteIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "遠端 IP 位址"),
                    RepairedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true, comment: "修復時間"),
                    Schema = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "資料表綱要"),
                    StoredHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "儲存雜湊值"),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "資料表名稱"),
                    Timestamp = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "時間戳記"),
                    TraceId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, comment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "使用者識別"),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "人員姓名")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => new { x.Id, x.CreatedAt });
                },
                comment: "資料稽核日誌實體 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.CreateTable(
                name: "SecurityLog",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "流水號主鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlertId = table.Column<string>(type: "char(32)", nullable: true, comment: "告警識別碼"),
                    ClientIp = table.Column<string>(type: "NVARCHAR(64)", maxLength: 64, nullable: true, comment: "客戶端來源 IP 位址"),
                    CorrelationId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, comment: "關聯識別碼"),
                    EventCategory = table.Column<string>(type: "NVARCHAR(128)", maxLength: 128, nullable: true, comment: "稽核事件分類 (例如: Auth.TokenRefresh)"),
                    Exception = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true, comment: "異常堆疊追蹤資訊"),
                    Fingerprint = table.Column<string>(type: "char(64)", nullable: true, comment: "特徵碼"),
                    Level = table.Column<string>(type: "NVARCHAR(128)", maxLength: 128, nullable: true, comment: "日誌層級 (例如: Information, Warning, Error)"),
                    LogType = table.Column<string>(type: "NVARCHAR(64)", maxLength: 64, nullable: true, comment: "日誌分流類型 (例如: Security)"),
                    Message = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true, comment: "日誌訊息"),
                    Properties = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true, comment: "隨附結構化快照內容 (JSON 格式)"),
                    Timestamp = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "高精度時間戳記 (帶時區)"),
                    UserId = table.Column<string>(type: "NVARCHAR(128)", maxLength: 128, nullable: true, comment: "觸發事件的用戶識別碼")
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
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "識別鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlertId = table.Column<string>(type: "char(32)", nullable: true, comment: "告警識別碼"),
                    ControllerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "MVC Controller 名稱"),
                    CorrelationId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, comment: "關聯識別碼"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "建立時間"),
                    CurrentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "雜湊值"),
                    Exception = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "異常"),
                    Fingerprint = table.Column<string>(type: "char(64)", nullable: true, comment: "特徵碼"),
                    IP = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true, comment: "IP位址"),
                    Level = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "層級"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "訊息"),
                    ModuleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "模組名稱"),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "操作"),
                    OperationDesc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true, comment: "操作說明 (ControllerTitle > FunctionTitle)"),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "有效載荷"),
                    PrevHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "前一筆雜湊值"),
                    TenantId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "區域實驗室關聯識別碼"),
                    TenantName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "實驗室名稱 (TenantName)"),
                    TimeStamp = table.Column<DateTimeOffset>(type: "DATETIMEOFFSET(7)", nullable: false, defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')", comment: "時間戳記"),
                    Url = table.Column<string>(type: "nvarchar(2083)", maxLength: 2083, nullable: true, comment: "URL"),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "用戶識別碼"),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "人員名稱")
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
                schema: "core",
                table: "AuditLogs",
                column: "IsRepaired",
                filter: "[IsRepaired] = 0")
                .Annotation("SqlServer:Include", new[] { "TableName", "TraceId", "GapReason" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TraceId_Covering",
                schema: "core",
                table: "AuditLogs",
                column: "TraceId")
                .Annotation("SqlServer:Include", new[] { "Action", "CreatedAt", "TableName" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLog_CorrelationId",
                schema: "core",
                table: "SecurityLog",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_CorrelationId",
                schema: "core",
                table: "SystemLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_TenantId",
                schema: "core",
                table: "SystemLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_TimeStamp",
                schema: "core",
                table: "SystemLogs",
                column: "TimeStamp");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_UserId",
                schema: "core",
                table: "SystemLogs",
                column: "UserId");
        }
    }
}
