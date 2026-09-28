using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_PermissionMetadata_ActionCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                comment: "用於區分該節點為一般檢視、業務操作或關鍵管理權限");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                schema: "core",
                table: "PermissionMetadata");
        }
    }
}
