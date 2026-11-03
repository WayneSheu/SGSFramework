using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_auditlog_time : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "AuditLogs",
                schema: "core",
                comment: "資料稽核日誌實體 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)",
                oldComment: "資料稽核日誌實體")
                .Annotation("SqlServer:IsLedger", true)
                .Annotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "core",
                table: "AuditLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                comment: "時間戳記",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()",
                oldComment: "時間戳記");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "AuditLogs",
                type: "DATETIMEOFFSET(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()",
                oldComment: "建立時間");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "AuditLogs",
                schema: "core",
                comment: "資料稽核日誌實體",
                oldComment: "資料稽核日誌實體 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)")
                .OldAnnotation("SqlServer:IsLedger", true)
                .OldAnnotation("SqlServer:IsLedgerAppendOnly", true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                schema: "core",
                table: "AuditLogs",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                comment: "時間戳記",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldComment: "時間戳記");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "core",
                table: "AuditLogs",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                comment: "建立時間",
                oldClrType: typeof(DateTimeOffset),
                oldType: "DATETIMEOFFSET(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'",
                oldComment: "建立時間");
        }
    }
}
