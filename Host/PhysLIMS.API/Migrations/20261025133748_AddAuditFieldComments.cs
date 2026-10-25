using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RefreshTokenHash",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                comment: "當前有效的 Refresh Token 雜湊值 (SHA256，避免明文外洩)",
                oldClrType: typeof(string),
                oldType: "nchar(64)",
                oldFixedLength: true,
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "UserLabMappings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "最後更新人員識別鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "最後更新人員識別鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "datetimeoffset(7)",
                nullable: true,
                comment: "最後更新時間 (UTC)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "建立人員識別鍵",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                comment: "建立時間 (UTC)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RefreshTokenHash",
                schema: "core",
                table: "UserRefreshTokens",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(64)",
                oldFixedLength: true,
                oldMaxLength: 64,
                oldComment: "當前有效的 Refresh Token 雜湊值 (SHA256，避免明文外洩)");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "UserLabMappings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "最後更新人員識別鍵");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "最後更新人員識別鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "datetimeoffset(7)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true,
                oldComment: "最後更新時間 (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "建立人員識別鍵");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()",
                oldComment: "建立時間 (UTC)");
        }
    }
}
