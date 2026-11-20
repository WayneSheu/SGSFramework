using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.MAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MANDB_GenerationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GenerationJobs",
                schema: "man",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "功能代碼"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "功能標題"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, comment: "0 等待中；1 執行中；2 成功；3 失敗"),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "使用的視覺模型"),
                    Overwrite = table.Column<bool>(type: "bit", nullable: false, comment: "是否允許覆寫既有主題"),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "生成結果對應的手冊主題（成功時）"),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "失敗原因或提示訊息（已去除模型原始輸出）"),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true, comment: "執行耗時（毫秒）"),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationJobs", x => x.Id);
                },
                comment: "操作手冊 AI 生成作業紀錄（不含截圖內容）");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_CreatedAtUtc",
                schema: "man",
                table: "GenerationJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_Status",
                schema: "man",
                table: "GenerationJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GenerationJobs",
                schema: "man");
        }
    }
}
