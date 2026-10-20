using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleLabScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleLabScopes",
                schema: "core",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleLabScopes", x => new { x.RoleId, x.TenantLabId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleLabScopes_TenantLabId",
                schema: "core",
                table: "RoleLabScopes",
                column: "TenantLabId");

            // 既有實驗室專屬角色：將單一 TenantLabId 轉入多實驗室對應表
            migrationBuilder.Sql(@"
INSERT INTO [core].[RoleLabScopes] ([RoleId], [TenantLabId])
SELECT [Id], [TenantLabId] FROM [core].[AspNetRoles]
WHERE [TenantLabId] IS NOT NULL AND [TenantLabId] <> '00000000-0000-0000-0000-000000000000';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleLabScopes",
                schema: "core");
        }
    }
}
