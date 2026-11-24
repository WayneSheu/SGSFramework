using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLabAuditAssignmentUpdateTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UpdateNote",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "最後一次修改的異動摘要（例：期間、稽核員、實驗室變更前後）");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "core",
                table: "LabAuditAssignments",
                type: "datetimeoffset",
                nullable: true,
                comment: "最後修改時間（未曾修改為 null）");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                schema: "core",
                table: "LabAuditAssignments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "最後修改人員名稱");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdateNote",
                schema: "core",
                table: "LabAuditAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "core",
                table: "LabAuditAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                schema: "core",
                table: "LabAuditAssignments");
        }
    }
}
