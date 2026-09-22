using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class ControllerMetadataAddComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "ControllerMetadatas",
                schema: "core",
                comment: "API 控制器與 Action 中繼資料實體");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "ControllerMetadatas",
                schema: "core",
                oldComment: "API 控制器與 Action 中繼資料實體");
        }
    }
}
