using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_MonthlyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyReports",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Period = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false, comment: "統計月份（YYYY-MM，台灣時間）"),
                    Revision = table.Column<int>(type: "int", nullable: false, comment: "版次：重新產生時遞增"),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "FMS 檔案識別碼（pms-form）"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    TotalIncoming = table.Column<int>(type: "int", nullable: false, comment: "總進線單數"),
                    SlaOnTimeRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true, comment: "SLA 準時達成率（0~1；無適用單據為空）"),
                    MttrHours = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true, comment: "平均修復時間 MTTR（小時；當月無修復完成的異常單為空）"),
                    OverdueCount = table.Column<int>(type: "int", nullable: false, comment: "統計基準時點已逾期的未結案單數"),
                    DueSoonCount = table.Column<int>(type: "int", nullable: false),
                    GeneratedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyReports_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "月度維運報告：統計快照（進線數、SLA 準時達成率、MTTR、逾期數）與 PDF 索引（實體檔案在 FMS）");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyReports_ProjectId_Period",
                schema: "pms",
                table: "MonthlyReports",
                columns: new[] { "ProjectId", "Period" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyReports",
                schema: "pms");
        }
    }
}
