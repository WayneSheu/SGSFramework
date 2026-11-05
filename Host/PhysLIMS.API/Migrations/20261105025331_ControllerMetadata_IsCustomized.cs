using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class ControllerMetadata_IsCustomized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCustomized",
                schema: "core",
                table: "ControllerMetadatas",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否由管理者自訂過功能名稱、說明、排序、圖示；啟動同步不會覆蓋這些欄位。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCustomized",
                schema: "core",
                table: "ControllerMetadatas");
        }
    }
}
