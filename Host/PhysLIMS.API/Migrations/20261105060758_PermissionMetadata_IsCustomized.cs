using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class PermissionMetadata_IsCustomized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCustomized",
                schema: "core",
                table: "PermissionMetadata",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否由管理者自訂過操作標題與說明；啟動同步不會覆蓋這些欄位。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCustomized",
                schema: "core",
                table: "PermissionMetadata");
        }
    }
}
