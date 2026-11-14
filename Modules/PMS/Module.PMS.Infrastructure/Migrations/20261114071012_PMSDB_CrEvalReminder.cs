using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_CrEvalReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEvalReminderAt",
                schema: "pms",
                table: "ChangeRequests",
                type: "datetimeoffset",
                nullable: true,
                comment: "最近一次評估逾期提醒時間（同一張 CR 每個台灣日最多提醒一次；重啟時清除）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastEvalReminderAt",
                schema: "pms",
                table: "ChangeRequests");
        }
    }
}
