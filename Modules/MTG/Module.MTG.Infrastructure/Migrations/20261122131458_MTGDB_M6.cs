using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.MTG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MTGDB_M6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiredAtUtc",
                schema: "mtg",
                table: "MeetingAudios",
                type: "datetimeoffset",
                nullable: true,
                comment: "錄音依保存期限（Meeting:AudioRetentionDays）從 FMS 刪除的時間（UTC）；未過期為空");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PmsSyncedAtUtc",
                schema: "mtg",
                table: "ActionItems",
                type: "datetimeoffset",
                nullable: true,
                comment: "回寫為 PMS 任務的時間（UTC）");

            migrationBuilder.AddColumn<Guid>(
                name: "PmsTaskId",
                schema: "mtg",
                table: "ActionItems",
                type: "uniqueidentifier",
                nullable: true,
                comment: "已回寫為 PMS 專案 WBS 任務時的任務識別碼；未回寫為空。回寫後此待辦視為已處理，重新抽取不會刪除");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiredAtUtc",
                schema: "mtg",
                table: "MeetingAudios");

            migrationBuilder.DropColumn(
                name: "PmsSyncedAtUtc",
                schema: "mtg",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "PmsTaskId",
                schema: "mtg",
                table: "ActionItems");
        }
    }
}
