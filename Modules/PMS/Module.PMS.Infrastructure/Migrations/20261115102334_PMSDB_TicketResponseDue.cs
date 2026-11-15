using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_TicketResponseDue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstResponseDueAt",
                schema: "pms",
                table: "Tickets",
                type: "datetimeoffset",
                nullable: true,
                comment: "首次回應期限（專案綁定履約中合約時，依附件二回應時間以標準服務時間計；否則為空，畫面以系統預設估算）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstResponseDueAt",
                schema: "pms",
                table: "Tickets");
        }
    }
}
