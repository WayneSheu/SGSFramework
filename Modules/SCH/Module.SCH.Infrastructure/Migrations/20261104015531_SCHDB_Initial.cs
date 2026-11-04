using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.SCH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SCHDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sch");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "sch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "稽核日誌識別主鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TraceId = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "使用者識別"),
                    RemoteIp = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "遠端 IP 位址"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "時間戳記"),
                    Schema = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "資料表綱要"),
                    TableName = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "資料表名稱"),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "操作類型"),
                    KeyValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "主鍵值"),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "舊值"),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "新值"),
                    ChangedColumns = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "變更欄位"),
                    PreviousHash = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "前一筆雜湊值"),
                    StoredHash = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "儲存雜湊值"),
                    IsRepaired = table.Column<bool>(type: "bit", nullable: false, comment: "是否已修復"),
                    RepairedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "修復時間"),
                    GapReason = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "差異原因"),
                    OriginalStoredHash = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "原始儲存雜湊值")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                },
                comment: "資料稽核日誌實體");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "sch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    CorrelationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true, comment: "關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)"),
                    CausationId = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true, comment: "誘發 ID：代表觸發此事件的具體 Command 名稱或 ID"),
                    Type = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false, comment: "事件的完整型別名稱 (包含 Namespace)，用於還原物件"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "事件序列化後的 JSON 內容"),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "事件發生的原始時間 (UTC)"),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。"),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。"),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "目前已重試的次數"),
                    IsDead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。"),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                },
                comment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.CreateIndex(
                name: "IX_sch_OutboxMessages_CorrelationId",
                schema: "sch",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_sch_OutboxMessages_FetchPending",
                schema: "sch",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "IsDead", "ScheduledAtUtc" });

            // 排程資料表原屬 Host 的 core schema（PhysLIMSDbContext），由本模組接管：
            // 1. core 仍有資料表且 sch 尚未建立 → 以 ALTER SCHEMA TRANSFER 原表移入（保留資料、索引與欄位說明）
            // 2. 仍不存在（全新資料庫且 Host 尚未建立）→ 建立新表
            // Host migration MoveScheduledJobTablesToSch 以相同規則處理，兩者執行順序不影響結果。
            // ※ 重新產生此 migration 時須保留以下守衛式 SQL，不可改回 CreateTable。
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[core].[ScheduledJobRuns]', N'U') IS NOT NULL AND OBJECT_ID(N'[sch].[ScheduledJobRuns]', N'U') IS NULL
                    ALTER SCHEMA [sch] TRANSFER [core].[ScheduledJobRuns];
                IF OBJECT_ID(N'[core].[ScheduledJobSettings]', N'U') IS NOT NULL AND OBJECT_ID(N'[sch].[ScheduledJobSettings]', N'U') IS NULL
                    ALTER SCHEMA [sch] TRANSFER [core].[ScheduledJobSettings];
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[sch].[ScheduledJobRuns]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [sch].[ScheduledJobRuns] (
                        [Id] uniqueidentifier NOT NULL,
                        [JobId] nvarchar(100) NOT NULL,
                        [JobName] nvarchar(200) NOT NULL,
                        [HangfireJobId] nvarchar(50) NOT NULL,
                        [Attempt] int NOT NULL,
                        [Trigger] nvarchar(20) NOT NULL,
                        [TriggeredBy] nvarchar(256) NULL,
                        [Status] nvarchar(20) NOT NULL,
                        [StartedAt] datetimeoffset NOT NULL,
                        [FinishedAt] datetimeoffset NULL,
                        [DurationMs] bigint NULL,
                        [Message] nvarchar(2000) NULL,
                        [ServerName] nvarchar(200) NULL,
                        CONSTRAINT [PK_ScheduledJobRuns] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_ScheduledJobRuns_HangfireJobId_Attempt] ON [sch].[ScheduledJobRuns] ([HangfireJobId], [Attempt]);
                    CREATE INDEX [IX_ScheduledJobRuns_JobId_StartedAt] ON [sch].[ScheduledJobRuns] ([JobId], [StartedAt]);
                    CREATE INDEX [IX_ScheduledJobRuns_StartedAt] ON [sch].[ScheduledJobRuns] ([StartedAt]);
                    EXEC sp_addextendedproperty 'MS_Description', N'排程作業執行紀錄：每次嘗試一筆（重試為新的一筆）', 'SCHEMA', N'sch', 'TABLE', N'ScheduledJobRuns';
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[sch].[ScheduledJobSettings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [sch].[ScheduledJobSettings] (
                        [JobId] nvarchar(100) NOT NULL,
                        [IsPaused] bit NOT NULL,
                        [PauseReason] nvarchar(500) NULL,
                        [Cron] nvarchar(100) NULL,
                        [TimeZone] nvarchar(100) NULL,
                        [CreatedBy] nvarchar(256) NOT NULL,
                        [CreatedAtUtc] datetimeoffset NOT NULL,
                        [UpdatedBy] nvarchar(256) NULL,
                        [UpdatedAtUtc] datetimeoffset NULL,
                        CONSTRAINT [PK_ScheduledJobSettings] PRIMARY KEY ([JobId])
                    );
                    EXEC sp_addextendedproperty 'MS_Description', N'排程作業的執行期設定：由排程管理頁暫停 / 啟動，系統重啟後仍維持', 'SCHEMA', N'sch', 'TABLE', N'ScheduledJobSettings';
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "sch");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "sch");

            migrationBuilder.DropTable(
                name: "ScheduledJobRuns",
                schema: "sch");

            migrationBuilder.DropTable(
                name: "ScheduledJobSettings",
                schema: "sch");
        }
    }
}
