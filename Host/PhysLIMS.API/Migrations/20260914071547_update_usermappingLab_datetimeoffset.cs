using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_usermappingLab_datetimeoffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiryDate",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveDate",
                schema: "core",
                table: "UserLabMappings",
                type: "datetimeoffset(7)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiryDate",
                schema: "core",
                table: "UserLabMappings",
                type: "datetime2(7)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EffectiveDate",
                schema: "core",
                table: "UserLabMappings",
                type: "datetime2(7)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)");
        }
    }
}
