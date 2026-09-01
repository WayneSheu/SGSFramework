using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_controller_metadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BitPosition",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttributesJson",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "BitPosition",
                schema: "core",
                table: "ControllerMetadatas");
        }
    }
}
