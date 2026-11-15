using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractSupportReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportSettingsReason",
                schema: "pms",
                table: "Contracts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "最近一次調整非服務時間支援參數的原因");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SupportSettingsUpdatedAt",
                schema: "pms",
                table: "Contracts",
                type: "datetimeoffset",
                nullable: true,
                comment: "最近一次調整非服務時間支援參數的時間");

            migrationBuilder.AddColumn<string>(
                name: "SupportSettingsUpdatedBy",
                schema: "pms",
                table: "Contracts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                comment: "最近一次調整非服務時間支援參數的人");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportSettingsReason",
                schema: "pms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SupportSettingsUpdatedAt",
                schema: "pms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SupportSettingsUpdatedBy",
                schema: "pms",
                table: "Contracts");
        }
    }
}
