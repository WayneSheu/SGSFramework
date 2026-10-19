using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_UserRefreshTokenConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRefreshTokens_RefreshTokenHash",
                schema: "core",
                table: "UserRefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserRefreshTokens_UserId_DeviceId_IsDead",
                schema: "core",
                table: "UserRefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_UserRefreshTokens_Lockdown_Rotation_Seek",
                schema: "core",
                table: "UserRefreshTokens",
                columns: new[] { "UserId", "DeviceId", "RefreshTokenHash", "IsDead", "IsFrozen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRefreshTokens_Lockdown_Rotation_Seek",
                schema: "core",
                table: "UserRefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_UserRefreshTokens_RefreshTokenHash",
                schema: "core",
                table: "UserRefreshTokens",
                column: "RefreshTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_UserRefreshTokens_UserId_DeviceId_IsDead",
                schema: "core",
                table: "UserRefreshTokens",
                columns: new[] { "UserId", "DeviceId", "IsDead" });
        }
    }
}
