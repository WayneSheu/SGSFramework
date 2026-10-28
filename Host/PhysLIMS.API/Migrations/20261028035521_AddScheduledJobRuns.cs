using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledJobRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledJobRuns",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    JobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "週期作業 ID（例如 database-backup-full）"),
                    JobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "執行當下的作業顯示名稱"),
                    HangfireJobId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "Hangfire 背景作業 ID（同一作業的重試共用）"),
                    Attempt = table.Column<int>(type: "int", nullable: false, comment: "第幾次嘗試（1 = 首次執行）"),
                    Trigger = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "觸發方式：Schedule（排程）或 Manual（手動立即執行）"),
                    TriggeredBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "手動執行的人員帳號；排程觸發為 null"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "執行狀態：Running / Succeeded / Failed / Retrying"),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "開始時間"),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "結束時間"),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true, comment: "耗時（毫秒）"),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "失敗原因或重試說明"),
                    ServerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "執行作業的伺服器（多節點時追查用）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobRuns", x => x.Id);
                },
                comment: "排程作業執行紀錄：每次嘗試一筆（重試為新的一筆）");

            migrationBuilder.CreateTable(
                name: "ScheduledJobSettings",
                schema: "core",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "週期作業 ID（例如 database-backup-full）"),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false, comment: "是否暫停自動排程（暫停時仍可手動立即執行）"),
                    PauseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "暫停原因"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後暫停 / 啟動的人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後暫停 / 啟動的時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobSettings", x => x.JobId);
                },
                comment: "排程作業的執行期設定：由排程管理頁暫停 / 啟動，系統重啟後仍維持");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobRuns_HangfireJobId_Attempt",
                schema: "core",
                table: "ScheduledJobRuns",
                columns: new[] { "HangfireJobId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobRuns_JobId_StartedAt",
                schema: "core",
                table: "ScheduledJobRuns",
                columns: new[] { "JobId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobRuns_StartedAt",
                schema: "core",
                table: "ScheduledJobRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledJobRuns",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ScheduledJobSettings",
                schema: "core");
        }
    }
}
