using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledJobScheduleOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cron",
                schema: "core",
                table: "ScheduledJobSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "排程管理頁設定的 Cron（5 碼）；null 表示沿用組態 / 程式預設");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "core",
                table: "ScheduledJobSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "排程管理頁設定的時區（IANA 或 Windows ID）；null 表示沿用組態預設");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cron",
                schema: "core",
                table: "ScheduledJobSettings");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "core",
                table: "ScheduledJobSettings");
        }
    }
}
