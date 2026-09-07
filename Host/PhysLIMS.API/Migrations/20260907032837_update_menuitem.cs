using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_menuitem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_DisplayOrder",
                schema: "core",
                table: "MenuItems");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Component",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                schema: "core",
                table: "MenuItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Key",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Key",
                schema: "core",
                table: "MenuItems",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ModuleName_IsActive_DisplayOrder",
                schema: "core",
                table: "MenuItems",
                columns: new[] { "ModuleName", "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_Key",
                schema: "core",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_ModuleName_IsActive_DisplayOrder",
                schema: "core",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Component",
                schema: "core",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "IsVisible",
                schema: "core",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Key",
                schema: "core",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ModuleName",
                schema: "core",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "core",
                table: "MenuItems");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "MenuItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_DisplayOrder",
                schema: "core",
                table: "MenuItems",
                column: "DisplayOrder");
        }
    }
}
