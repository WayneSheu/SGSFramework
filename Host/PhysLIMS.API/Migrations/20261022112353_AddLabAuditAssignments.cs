using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLabAuditAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresAuditAssignment",
                schema: "core",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LabAuditAssignments",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RevokeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabAuditAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabAuditAssignments_TenantLabId",
                schema: "core",
                table: "LabAuditAssignments",
                column: "TenantLabId");

            migrationBuilder.CreateIndex(
                name: "IX_LabAuditAssignments_UserId_TenantLabId",
                schema: "core",
                table: "LabAuditAssignments",
                columns: new[] { "UserId", "TenantLabId" });

            // 既有「各區實驗室稽核員」(AU) 設為稽核角色：其權限改為僅於有效稽核指派的實驗室生效
            migrationBuilder.Sql(@"
UPDATE [core].[AspNetRoles] SET [RequiresAuditAssignment] = 1
WHERE [Code] = 'AU' AND [IsDeleted] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabAuditAssignments",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "RequiresAuditAssignment",
                schema: "core",
                table: "AspNetRoles");
        }
    }
}
