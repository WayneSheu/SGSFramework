using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_RcaReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RcaReports",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Trigger = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "事件摘要"),
                    RecoveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "系統恢復時間（提交期限自此起算 48 小時）"),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "提交期限＝系統恢復後 48 小時"),
                    TimelineJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "精確時間軸（JSON：[{\"at\":…,\"event\":…}]）"),
                    RootCause = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "技術根因（敏感）"),
                    ShortTermAction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "短期止血措施"),
                    LongTermPrevention = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "長期預防措施"),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "寄送客戶的備註（寄送對象、方式）"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RcaReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RcaReports_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "pms",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "P1 重大異常分析報告：事件摘要、時間軸、根因、短期止血與長期預防；系統恢復後 48 小時內提交");

            migrationBuilder.CreateIndex(
                name: "IX_RcaReports_ProjectId_Status_DueAt",
                schema: "pms",
                table: "RcaReports",
                columns: new[] { "ProjectId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RcaReports_TicketId",
                schema: "pms",
                table: "RcaReports",
                column: "TicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RcaReports",
                schema: "pms");
        }
    }
}
