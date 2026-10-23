using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class ControllerMetadata_IsManuallyDisabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyDisabled",
                schema: "core",
                table: "ControllerMetadatas",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否由管理者手動停用；啟動同步或模組重新啟用時不會覆蓋為啟用。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyDisabled",
                schema: "core",
                table: "ControllerMetadatas");
        }
    }
}
