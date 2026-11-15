using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractDeliveredChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DeliveredNote",
                schema: "pms",
                table: "ContractDeliverableItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "交付備註（對象、網址、版本等）",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "交付備註（交付方式、對象等）");

            migrationBuilder.AddColumn<string>(
                name: "DeliveredChannel",
                schema: "pms",
                table: "ContractDeliverableItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "交付方式（Email、系統上線、紙本面交、雲端連結等）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredChannel",
                schema: "pms",
                table: "ContractDeliverableItems");

            migrationBuilder.AlterColumn<string>(
                name: "DeliveredNote",
                schema: "pms",
                table: "ContractDeliverableItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "交付備註（交付方式、對象等）",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "交付備註（對象、網址、版本等）");
        }
    }
}
