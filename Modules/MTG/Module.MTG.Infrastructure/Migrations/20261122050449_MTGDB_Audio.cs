using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.MTG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MTGDB_Audio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClientSegmentId",
                schema: "mtg",
                table: "TranscriptSegments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "桌面端產生的冪等識別碼（用於去重；可空）",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "桌面端產生的冪等識別碼（M2 起用於去重；可空）");

            migrationBuilder.AddColumn<Guid>(
                name: "AudioId",
                schema: "mtg",
                table: "TranscriptSegments",
                type: "uniqueidentifier",
                nullable: true,
                comment: "由哪個音檔轉錄而來（可空：貼上或桌面端上傳的片段沒有來源音檔）；重新轉錄時只取代同一音檔的片段");

            migrationBuilder.CreateTable(
                name: "MeetingAudios",
                schema: "mtg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "所屬會議"),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "檔案管理（FMS）的檔案識別碼"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false, comment: "上傳時的原始檔名"),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "FMS 依檔頭簽章判定的 MIME 類型"),
                    FileSize = table.Column<long>(type: "bigint", nullable: false, comment: "位元組"),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "原始內容雜湊（十六進位）"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "Queued、Transcribing、Transcribed、Failed"),
                    StatusMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "失敗原因（不含逐字稿內容）"),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true, comment: "秒；轉錄服務回報時才有"),
                    Language = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, comment: "轉錄服務回報的語言代碼（可空）"),
                    SegmentCount = table.Column<int>(type: "int", nullable: false, comment: "此音檔轉錄出的逐字稿片段數"),
                    TranscribedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "UTC"),
                    AutoSummarize = table.Column<bool>(type: "bit", nullable: false, comment: "上傳時是否勾選；轉錄成功後由工作者接續要求產生紀要"),
                    SummarizeScenarioCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "自動產生紀要時採用的場景代碼（可空＝沿用會議場景）"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "上傳人員"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "上傳時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後異動人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後異動時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAudios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAudios_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "mtg",
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "會議錄音：實際檔案以加密形式存放於檔案管理（FMS），此表只存關聯與轉錄狀態");

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSegments_AudioId",
                schema: "mtg",
                table: "TranscriptSegments",
                column: "AudioId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAudios_FileId",
                schema: "mtg",
                table: "MeetingAudios",
                column: "FileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAudios_MeetingId",
                schema: "mtg",
                table: "MeetingAudios",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAudios_Status",
                schema: "mtg",
                table: "MeetingAudios",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingAudios",
                schema: "mtg");

            migrationBuilder.DropIndex(
                name: "IX_TranscriptSegments_AudioId",
                schema: "mtg",
                table: "TranscriptSegments");

            migrationBuilder.DropColumn(
                name: "AudioId",
                schema: "mtg",
                table: "TranscriptSegments");

            migrationBuilder.AlterColumn<string>(
                name: "ClientSegmentId",
                schema: "mtg",
                table: "TranscriptSegments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "桌面端產生的冪等識別碼（M2 起用於去重；可空）",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "桌面端產生的冪等識別碼（用於去重；可空）");
        }
    }
}
