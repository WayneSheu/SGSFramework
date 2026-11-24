using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLabAuditAssignmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabAuditAssignmentHistories",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "稽核指派識別鍵（LabAuditAssignment.Id）"),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "事件類型：created / updated / revoked"),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "操作人員識別鍵"),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "操作人員名稱"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "發生時間"),
                    Summary = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false, comment: "異動摘要（建立：稽核員 / 實驗室 / 期間 / 原因；修改：變更前後；撤銷：撤銷原因）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabAuditAssignmentHistories", x => x.Id);
                },
                comment: "實驗室稽核指派異動歷程：每次建立、修改、撤銷各留一筆不可變更的事件，保留完整稽核軌跡（LabAuditAssignment 本身只保留最新狀態）。");

            migrationBuilder.CreateIndex(
                name: "IX_LabAuditAssignmentHistories_AssignmentId_OccurredAt",
                schema: "core",
                table: "LabAuditAssignmentHistories",
                columns: new[] { "AssignmentId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabAuditAssignmentHistories",
                schema: "core");
        }
    }
}
