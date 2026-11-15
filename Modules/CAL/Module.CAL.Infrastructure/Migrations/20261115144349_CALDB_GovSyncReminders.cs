using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.CAL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CALDB_GovSyncReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Source",
                schema: "cal",
                table: "Holidays",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                comment: "資料來源：0 人工維護；1 政府辦公日曆同步（同步只會覆寫與移除自己建立的資料）");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "cal",
                table: "CalendarEvents",
                type: "uniqueidentifier",
                nullable: true,
                comment: "建立者使用者識別碼（提醒信收件人）；系統同步的活動為空");

            migrationBuilder.CreateTable(
                name: "CalendarEventAttendees",
                schema: "cal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "與會者使用者識別碼")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventAttendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventAttendees_CalendarEvents_EventId",
                        column: x => x.EventId,
                        principalSchema: "cal",
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "活動與會者：提醒信的收件人之一（另含活動建立者）");

            migrationBuilder.CreateTable(
                name: "CalendarEventReminders",
                schema: "cal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinutesBefore = table.Column<int>(type: "int", nullable: false, comment: "活動開始前幾分鐘提醒（1~20160）"),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "提醒信寄出時間；空表示尚未寄出（活動改期會重設）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventReminders_CalendarEvents_EventId",
                        column: x => x.EventId,
                        principalSchema: "cal",
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "活動提醒：開始前 N 分鐘寄 Email，寄出後記錄時間避免重複");

            migrationBuilder.CreateTable(
                name: "HolidaySyncRuns",
                schema: "cal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false, comment: "同步的西元年度"),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "執行者（使用者或排程帳號）"),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "資料來源網址"),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false, comment: "是否成功"),
                    Added = table.Column<int>(type: "int", nullable: false),
                    Updated = table.Column<int>(type: "int", nullable: false),
                    Removed = table.Column<int>(type: "int", nullable: false),
                    SkippedManual = table.Column<int>(type: "int", nullable: false, comment: "因人工維護而略過的筆數（排程不覆寫人工資料）"),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "結果說明或失敗原因"),
                    RunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidaySyncRuns", x => x.Id);
                },
                comment: "政府辦公日曆同步紀錄：年度、方式、來源網址與新增／更新／移除筆數");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventAttendees_EventId_UserId",
                schema: "cal",
                table: "CalendarEventAttendees",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventReminders_EventId_MinutesBefore",
                schema: "cal",
                table: "CalendarEventReminders",
                columns: new[] { "EventId", "MinutesBefore" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventReminders_SentAtUtc",
                schema: "cal",
                table: "CalendarEventReminders",
                column: "SentAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HolidaySyncRuns_RunAtUtc",
                schema: "cal",
                table: "HolidaySyncRuns",
                column: "RunAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEventAttendees",
                schema: "cal");

            migrationBuilder.DropTable(
                name: "CalendarEventReminders",
                schema: "cal");

            migrationBuilder.DropTable(
                name: "HolidaySyncRuns",
                schema: "cal");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "cal",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "cal",
                table: "CalendarEvents");
        }
    }
}
