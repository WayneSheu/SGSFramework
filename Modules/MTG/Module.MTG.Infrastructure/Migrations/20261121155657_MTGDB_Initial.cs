using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.MTG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MTGDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mtg");

            migrationBuilder.CreateTable(
                name: "GlossaryEntries",
                schema: "mtg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    Term = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "正確的標準寫法（例如 Kubernetes）"),
                    Variants = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "常見誤聽或寫法，以 | 分隔（例如 酷伯|k8s）"),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "分類（可空）"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, comment: "停用後不參與校正"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後異動人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後異動時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlossaryEntries", x => x.Id);
                },
                comment: "企業術語與同音誤聽校正表");

            migrationBuilder.CreateTable(
                name: "Meetings",
                schema: "mtg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "會議標題"),
                    ScenarioCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "會議場景代碼（對應 PromptTemplate.ScenarioCode，例如 general、client-discovery）"),
                    MeetingTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "會議開始時間（UTC）"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯的 PMS 專案識別碼（可空；M5 起使用）"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "處理狀態：Draft、TranscriptReady、Summarizing、Done、Failed"),
                    StatusMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "失敗原因等狀態說明（不含逐字稿或模型內容）"),
                    Organizer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立會議的使用者"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後異動人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後異動時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                },
                comment: "會議主檔：標題、場景、處理狀態與可選的 PMS 專案關聯");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "mtg",
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

            migrationBuilder.CreateTable(
                name: "PromptTemplates",
                schema: "mtg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    ScenarioCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "場景代碼（唯一，小寫英數與連字號）"),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "場景顯示名稱"),
                    MapPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "Map 階段：自逐字稿片段提取重點的指令"),
                    ReducePrompt = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "Reduce 階段：最終 Markdown 輸出骨架與整合指令"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, comment: "停用後不可被選用"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "每次修改遞增"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後異動人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後異動時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptTemplates", x => x.Id);
                },
                comment: "會議場景的 Map / Reduce 提示詞模板");

            migrationBuilder.CreateTable(
                name: "MeetingMinutes",
                schema: "mtg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "所屬會議"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "紀要版本（從 1 起）"),
                    ScenarioCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "產生時採用的場景代碼"),
                    Markdown = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "Markdown 紀要"),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "產生紀要的模型名稱"),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "產生時間（UTC）"),
                    GeneratedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "觸發產生的使用者")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMinutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMinutes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "mtg",
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "會議紀要版本：每次產生新增一版，保留歷史");

            migrationBuilder.CreateTable(
                name: "TranscriptSegments",
                schema: "mtg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "所屬會議"),
                    Seq = table.Column<int>(type: "int", nullable: false, comment: "片段序號（從 1 起，會議內唯一）"),
                    Speaker = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "講者標籤（我方、與會者 A…；可空）"),
                    StartSeconds = table.Column<double>(type: "float", nullable: true, comment: "相對會議開始的起始秒數（可空）"),
                    EndSeconds = table.Column<double>(type: "float", nullable: true, comment: "相對會議開始的結束秒數（可空）"),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "原始逐字稿文字"),
                    CorrectedText = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "術語表校正後的文字"),
                    ClientSegmentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "桌面端產生的冪等識別碼（M2 起用於去重；可空）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranscriptSegments_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "mtg",
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "逐字稿片段：依序號排列，保留原文與術語校正後文字");

            migrationBuilder.CreateIndex(
                name: "IX_GlossaryEntries_Term",
                schema: "mtg",
                table: "GlossaryEntries",
                column: "Term",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMinutes_MeetingId_Version",
                schema: "mtg",
                table: "MeetingMinutes",
                columns: new[] { "MeetingId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_MeetingTimeUtc",
                schema: "mtg",
                table: "Meetings",
                column: "MeetingTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ProjectId",
                schema: "mtg",
                table: "Meetings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_Status",
                schema: "mtg",
                table: "Meetings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_mtg_OutboxMessages_CorrelationId",
                schema: "mtg",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_mtg_OutboxMessages_FetchPending",
                schema: "mtg",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "IsDead", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptTemplates_ScenarioCode",
                schema: "mtg",
                table: "PromptTemplates",
                column: "ScenarioCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSegments_MeetingId_ClientSegmentId",
                schema: "mtg",
                table: "TranscriptSegments",
                columns: new[] { "MeetingId", "ClientSegmentId" },
                unique: true,
                filter: "[ClientSegmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSegments_MeetingId_Seq",
                schema: "mtg",
                table: "TranscriptSegments",
                columns: new[] { "MeetingId", "Seq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlossaryEntries",
                schema: "mtg");

            migrationBuilder.DropTable(
                name: "MeetingMinutes",
                schema: "mtg");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "mtg");

            migrationBuilder.DropTable(
                name: "PromptTemplates",
                schema: "mtg");

            migrationBuilder.DropTable(
                name: "TranscriptSegments",
                schema: "mtg");

            migrationBuilder.DropTable(
                name: "Meetings",
                schema: "mtg");
        }
    }
}
