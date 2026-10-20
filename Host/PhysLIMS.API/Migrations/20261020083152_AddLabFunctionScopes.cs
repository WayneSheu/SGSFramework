using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLabFunctionScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabFunctionPolicies",
                schema: "core",
                columns: table => new
                {
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRestricted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabFunctionPolicies", x => x.TenantLabId);
                });

            migrationBuilder.CreateTable(
                name: "LabFunctionScopes",
                schema: "core",
                columns: table => new
                {
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabFunctionScopes", x => new { x.TenantLabId, x.PermissionKey });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabFunctionPolicies",
                schema: "core");

            migrationBuilder.DropTable(
                name: "LabFunctionScopes",
                schema: "core");
        }
    }
}
