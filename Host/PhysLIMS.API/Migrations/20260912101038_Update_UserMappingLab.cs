using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class Update_UserMappingLab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserLabMappings_User_Active_Primary",
                schema: "core",
                table: "UserLabMappings",
                columns: new[] { "UserId", "IsActive", "IsPrimary" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLabMappings_User_Active_Primary",
                schema: "core",
                table: "UserLabMappings");
        }
    }
}
