using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_menuitem_20260907 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_IsActive_IsVisible_DisplayOrder",
                schema: "core",
                table: "MenuItems",
                columns: new[] { "IsActive", "IsVisible", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_IsActive_IsVisible_DisplayOrder",
                schema: "core",
                table: "MenuItems");
        }
    }
}
