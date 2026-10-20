using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLabManagers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabManagers",
                schema: "core",
                columns: table => new
                {
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabManagers", x => x.TenantLabId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabManagers_ManagerUserId",
                schema: "core",
                table: "LabManagers",
                column: "ManagerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabManagers",
                schema: "core");
        }
    }
}
