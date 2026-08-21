using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_ModuleControllerMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModuleTitle",
                schema: "core",
                table: "ModuleMetadatas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ControllerTitle",
                schema: "core",
                table: "ControllerMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModuleTitle",
                schema: "core",
                table: "ModuleMetadatas");

            migrationBuilder.DropColumn(
                name: "ControllerTitle",
                schema: "core",
                table: "ControllerMetadata");
        }
    }
}
