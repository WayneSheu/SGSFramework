using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class systemlog_add_controllerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "MVC Controller 名稱");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ControllerName",
                schema: "core",
                table: "SystemLogs");
        }
    }
}
