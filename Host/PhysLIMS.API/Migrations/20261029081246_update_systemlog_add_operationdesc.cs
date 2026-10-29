using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_systemlog_add_operationdesc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Operation",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "操作",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "操作");

            migrationBuilder.AddColumn<string>(
                name: "OperationDesc",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                comment: "操作說明 (ControllerTitle > FunctionTitle)");

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "人員名稱");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperationDesc",
                schema: "core",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "UserName",
                schema: "core",
                table: "SystemLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Operation",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "操作",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "操作");
        }
    }
}
