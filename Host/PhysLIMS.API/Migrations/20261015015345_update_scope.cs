using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_scope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HierarchyLevel",
                schema: "core",
                table: "AspNetRoles",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<string>(
                name: "ScopeType",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "LabScoped");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantLabId",
                schema: "core",
                table: "AspNetRoles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoles_TenantLabId_HierarchyLevel",
                schema: "core",
                table: "AspNetRoles",
                columns: new[] { "TenantLabId", "HierarchyLevel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetRoles_TenantLabId_HierarchyLevel",
                schema: "core",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "HierarchyLevel",
                schema: "core",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                schema: "core",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "TenantLabId",
                schema: "core",
                table: "AspNetRoles");
        }
    }
}
