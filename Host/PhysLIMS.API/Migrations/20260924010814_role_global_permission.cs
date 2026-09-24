using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class role_global_permission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Role_Global_Permissions",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_id = table.Column<string>(type: "varchar(450)", unicode: false, maxLength: 450, nullable: false),
                    permission_key = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    bitmask = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role_Global_Permissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_role_global_permissions_role_key",
                schema: "core",
                table: "Role_Global_Permissions",
                columns: new[] { "role_id", "permission_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Role_Global_Permissions",
                schema: "core");
        }
    }
}
