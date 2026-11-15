using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractDeliverableDelivered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DeliveredDate",
                schema: "pms",
                table: "ContractDeliverableItems",
                type: "date",
                nullable: true,
                comment: "實際交付日（由業務／合約 PM 記錄；空表示尚未交付）");

            migrationBuilder.AddColumn<string>(
                name: "DeliveredNote",
                schema: "pms",
                table: "ContractDeliverableItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "交付備註（交付方式、對象等）");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReminderAt",
                schema: "pms",
                table: "ContractDeliverableItems",
                type: "datetimeoffset",
                nullable: true,
                comment: "最近一次到期提醒時間（每個台灣日最多一次）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredDate",
                schema: "pms",
                table: "ContractDeliverableItems");

            migrationBuilder.DropColumn(
                name: "DeliveredNote",
                schema: "pms",
                table: "ContractDeliverableItems");

            migrationBuilder.DropColumn(
                name: "LastReminderAt",
                schema: "pms",
                table: "ContractDeliverableItems");
        }
    }
}
