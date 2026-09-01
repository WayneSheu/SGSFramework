using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class user_lab_permission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "User_Global_Permissions",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    permission_key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    bitmask = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User_Global_Permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "User_Lab_Permissions",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    tenant_lab_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    controller_or_module_key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    bitmask = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User_Lab_Permissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_global_permissions_user_key",
                schema: "core",
                table: "User_Global_Permissions",
                columns: new[] { "user_id", "permission_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_lab_permissions_user_lab_controller",
                schema: "core",
                table: "User_Lab_Permissions",
                columns: new[] { "user_id", "tenant_lab_id", "controller_or_module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_lab_permissions_user_tenant",
                schema: "core",
                table: "User_Lab_Permissions",
                columns: new[] { "user_id", "tenant_lab_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "User_Global_Permissions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "User_Lab_Permissions",
                schema: "core");
        }
    }
}
