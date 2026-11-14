using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_Gantt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectBaselines",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false, comment: "基準線序號（專案內遞增，B1、B2…）"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBaselines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "專案排程基準線：任務起迄快照，供對照延後或提前");

            migrationBuilder.CreateTable(
                name: "ProjectTasks",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "上層任務（空為頂層）"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "同一上層下的排序（由小到大）"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "開始日（含）"),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "結束日（含）"),
                    IsMilestone = table.Column<bool>(type: "bit", nullable: false, comment: "是否為里程碑（起迄同一天）"),
                    AssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "負責人（須為專案內部成員）；空為未指派"),
                    PercentComplete = table.Column<byte>(type: "tinyint", nullable: false, comment: "完成百分比 0~100"),
                    Phase = table.Column<byte>(type: "tinyint", nullable: true, comment: "對應的 SSDLC 階段（選填，供階段分組）"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "專案 WBS 任務：階層、起迄日、負責人、進度；軟刪除");

            migrationBuilder.CreateTable(
                name: "ProjectBaselineTasks",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "拍照當時的任務識別碼（任務之後被刪除時仍可保留對照）"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsMilestone = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBaselineTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBaselineTasks_ProjectBaselines_BaselineId",
                        column: x => x.BaselineId,
                        principalSchema: "pms",
                        principalTable: "ProjectBaselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "基準線的任務快照（任務識別碼、名稱與起迄日）");

            migrationBuilder.CreateTable(
                name: "ProjectTaskDependencies",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredecessorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuccessorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LagDays = table.Column<int>(type: "int", nullable: false, comment: "落後天數（前置結束日後再等幾天；0＝次日即可開始）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTaskDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTaskDependencies_ProjectTasks_PredecessorId",
                        column: x => x.PredecessorId,
                        principalSchema: "pms",
                        principalTable: "ProjectTasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectTaskDependencies_ProjectTasks_SuccessorId",
                        column: x => x.SuccessorId,
                        principalSchema: "pms",
                        principalTable: "ProjectTasks",
                        principalColumn: "Id");
                },
                comment: "任務前置相依（FS）：後繼任務須在前置任務結束後（加落後天數）才開始");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBaselines_ProjectId_Number",
                schema: "pms",
                table: "ProjectBaselines",
                columns: new[] { "ProjectId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBaselineTasks_BaselineId_TaskId",
                schema: "pms",
                table: "ProjectBaselineTasks",
                columns: new[] { "BaselineId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskDependencies_PredecessorId_SuccessorId",
                schema: "pms",
                table: "ProjectTaskDependencies",
                columns: new[] { "PredecessorId", "SuccessorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskDependencies_ProjectId",
                schema: "pms",
                table: "ProjectTaskDependencies",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskDependencies_SuccessorId",
                schema: "pms",
                table: "ProjectTaskDependencies",
                column: "SuccessorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_AssigneeUserId",
                schema: "pms",
                table: "ProjectTasks",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ProjectId_IsDeleted_ParentId_SortOrder",
                schema: "pms",
                table: "ProjectTasks",
                columns: new[] { "ProjectId", "IsDeleted", "ParentId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectBaselineTasks",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ProjectTaskDependencies",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ProjectBaselines",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ProjectTasks",
                schema: "pms");
        }
    }
}
