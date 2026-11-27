using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddControllerMetadataModuleMenuInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModuleDomain",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "取自AssemblyInfo Module Attribute 的Domain；選單最上層標題。");

            migrationBuilder.AddColumn<int>(
                name: "ModuleDomainOrder",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "取自AssemblyInfo Module Attribute 的DomainOrder。");

            migrationBuilder.AddColumn<string>(
                name: "ModuleIcon",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "取自AssemblyInfo Module Attribute 的Icon；選單模組(Section)圖示。");

            migrationBuilder.AddColumn<int>(
                name: "ModuleOrder",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "取自AssemblyInfo Module Attribute 的Order；選單模組(Section)排序。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModuleDomain",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "ModuleDomainOrder",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "ModuleIcon",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "ModuleOrder",
                schema: "core",
                table: "ControllerMetadatas");
        }
    }
}
