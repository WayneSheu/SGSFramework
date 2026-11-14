using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_RiskItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskItems",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "風險標題"),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Mitigation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "因應措施（緩解、移轉、接受或迴避的做法）"),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "風險負責人（須為專案內部成員）"),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "狀態改為已結案的時間；重新開啟時清除"),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false, comment: "是否已作廢（軟刪除，保留稽核）"),
                    VoidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "專案風險：標題、等級、因應措施、負責人與狀態；作廢為軟刪除");

            migrationBuilder.CreateIndex(
                name: "IX_RiskItems_OwnerUserId",
                schema: "pms",
                table: "RiskItems",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskItems_ProjectId_IsVoided_Status_Level",
                schema: "pms",
                table: "RiskItems",
                columns: new[] { "ProjectId", "IsVoided", "Status", "Level" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskItems",
                schema: "pms");
        }
    }
}
