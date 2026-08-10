using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.ORG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class add_ORG_Labid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantLabId",
                schema: "org",
                table: "Organization",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organization_TenantLabId",
                schema: "org",
                table: "Organization",
                column: "TenantLabId",
                filter: "[TenantLabId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Organization_TenantLabId",
                schema: "org",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "TenantLabId",
                schema: "org",
                table: "Organization");
        }
    }
}
