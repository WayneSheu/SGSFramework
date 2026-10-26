using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditableToUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                comment: "建立時間 (UTC)");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                comment: "建立者");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true,
                comment: "最後修改時間 (UTC)");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "最後修改者");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "AspNetRoles",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                comment: "建立時間 (UTC)");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                comment: "建立者");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "AspNetRoles",
                type: "datetimeoffset",
                nullable: true,
                comment: "最後修改時間 (UTC)");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "AspNetRoles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "最後修改者");

            // 既有使用者以原有的建立時間回填，避免新欄位全為 0001-01-01；角色沒有舊的建立時間，維持預設值
            migrationBuilder.Sql("UPDATE [core].[AspNetUsers] SET [CreatedAtUtc] = [CreatedAt] WHERE [CreatedAtUtc] = '0001-01-01T00:00:00+00:00';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "core",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "core",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "core",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "core",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "core",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "core",
                table: "AspNetRoles");
        }
    }
}
