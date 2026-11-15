using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractSupportTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeHolidayRate",
                schema: "pms",
                table: "Contracts",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 2.5m,
                comment: "國定假日加班倍率（預設 2.5）");

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeNightRestRate",
                schema: "pms",
                table: "Contracts",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 2.0m,
                comment: "深夜與例假日加班倍率（預設 2.0）");

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeWeekdayRate",
                schema: "pms",
                table: "Contracts",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 1.5m,
                comment: "平日加班倍率（預設 1.5）");

            migrationBuilder.AddColumn<int>(
                name: "SupportAdvanceWorkdays",
                schema: "pms",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 5,
                comment: "非服務時間支援須提前申請的工作日數（預設 5）");

            migrationBuilder.AddColumn<int>(
                name: "SupportMaxWindowDays",
                schema: "pms",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 7,
                comment: "非服務時間支援單次窗口上限（天，預設 7）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OvertimeHolidayRate",
                schema: "pms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OvertimeNightRestRate",
                schema: "pms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "OvertimeWeekdayRate",
                schema: "pms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SupportAdvanceWorkdays",
                schema: "pms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SupportMaxWindowDays",
                schema: "pms",
                table: "Contracts");
        }
    }
}
