using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class FunctionStatus_NoticeAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                schema: "core",
                table: "ControllerMetadatas",
                type: "datetimeoffset",
                nullable: true,
                comment: "手動停用的時間 (UTC)。");

            migrationBuilder.AddColumn<string>(
                name: "DisabledNotice",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "手動停用時對使用者的公告內容。");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpectedRestoreAt",
                schema: "core",
                table: "ControllerMetadatas",
                type: "datetimeoffset",
                nullable: true,
                comment: "手動停用時公告的預計恢復時間 (UTC)，僅供公告不會自動恢復。");

            migrationBuilder.CreateTable(
                name: "FunctionStatusLogs",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControllerMetadataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControllerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notice = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpectedRestoreAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionStatusLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionStatusLogs_ControllerMetadataId_CreatedAt",
                schema: "core",
                table: "FunctionStatusLogs",
                columns: new[] { "ControllerMetadataId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FunctionStatusLogs",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "DisabledNotice",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "ExpectedRestoreAt",
                schema: "core",
                table: "ControllerMetadatas");
        }
    }
}
