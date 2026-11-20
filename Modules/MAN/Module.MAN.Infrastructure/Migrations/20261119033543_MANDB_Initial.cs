using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.MAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MANDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "man");

            migrationBuilder.CreateTable(
                name: "ManualTopics",
                schema: "man",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "功能代碼（唯一；前端 SmartTooltip 以此綁定，例如 export_finance_report）"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "主題標題"),
                    ModuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "所屬模組／章節名稱（用於手冊分章）"),
                    Tooltip = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "內嵌提示（30 字以內）"),
                    StepsMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "操作步驟（Markdown）"),
                    Precautions = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "注意事項（Markdown）"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "手冊排序"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0, comment: "0 草稿；1 已發布"),
                    Source = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0, comment: "0 人工；1 AI 生成草稿"),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualTopics", x => x.Id);
                },
                comment: "操作手冊主題：Tooltip、操作步驟與注意事項的單一來源");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "man",
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

            migrationBuilder.CreateIndex(
                name: "IX_ManualTopics_FeatureKey",
                schema: "man",
                table: "ManualTopics",
                column: "FeatureKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManualTopics_Status_SortOrder",
                schema: "man",
                table: "ManualTopics",
                columns: new[] { "Status", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualTopics",
                schema: "man");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "man");
        }
    }
}
