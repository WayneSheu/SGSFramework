using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ReleaseChecklists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReleaseChecklists",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, comment: "版更版本號（vX.Y.Z）"),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MainAuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "本次變更主要作者（交叉驗證者不得為此人）"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ExceptionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "例外放行備註（有警告項未完成時 Tech Lead 放行須填原因與預計補齊日）"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseChecklists_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "上線前資安與發布檢核表：版本、預計上線日、主要作者與三方放行簽核");

            migrationBuilder.CreateTable(
                name: "ReleaseChecklistItems",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, comment: "檢核編號（SEC-01…SEC-11、REL-01…REL-06）"),
                    Severity = table.Column<byte>(type: "tinyint", nullable: false),
                    Result = table.Column<byte>(type: "tinyint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "備註／證據位置（報告歸檔路徑等）"),
                    CheckedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseChecklistItems_ReleaseChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalSchema: "pms",
                        principalTable: "ReleaseChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "上線檢核項：編號、等級（建立時自範本快照）、結果與備註");

            migrationBuilder.CreateTable(
                name: "ReleaseSignoffs",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    SignerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExceptionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "Tech Lead 放行時的例外備註")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseSignoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseSignoffs_ReleaseChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalSchema: "pms",
                        principalTable: "ReleaseChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "上線放行簽核：交叉驗證、Tech Lead 放行、PM 確認");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseChecklistItems_ChecklistId_Code",
                schema: "pms",
                table: "ReleaseChecklistItems",
                columns: new[] { "ChecklistId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseChecklists_ProjectId_Status",
                schema: "pms",
                table: "ReleaseChecklists",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseChecklists_ProjectId_Version",
                schema: "pms",
                table: "ReleaseChecklists",
                columns: new[] { "ProjectId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseSignoffs_ChecklistId_Role",
                schema: "pms",
                table: "ReleaseSignoffs",
                columns: new[] { "ChecklistId", "Role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReleaseChecklistItems",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ReleaseSignoffs",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ReleaseChecklists",
                schema: "pms");
        }
    }
}
