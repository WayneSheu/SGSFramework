using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_systemlog_securitylog_time_taiwan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "TimeStamp",
                schema: "core",
                table: "SystemLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')",
                comment: "時間戳記",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldComment: "時間戳記");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "SystemLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')",
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "core",
                table: "SecurityLog",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')",
                comment: "高精度時間戳記 (帶時區)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldComment: "高精度時間戳記 (帶時區)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "TimeStamp",
                schema: "core",
                table: "SystemLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                comment: "時間戳記",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')",
                oldComment: "時間戳記");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "SystemLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')",
                oldComment: "建立時間");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "core",
                table: "SecurityLog",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                comment: "高精度時間戳記 (帶時區)",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "TODATETIMEOFFSET(SYSUTCDATETIME(), '+08:00')",
                oldComment: "高精度時間戳記 (帶時區)");
        }
    }
}
