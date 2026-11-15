using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ReviewRoundChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RespondedChannel",
                schema: "pms",
                table: "ContractReviewRounds",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "甲方簽認（回覆）的方式：書面簽回、Email、會議簽認、線上簽核等；系統自動擬制通過者為空");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RespondedChannel",
                schema: "pms",
                table: "ContractReviewRounds");
        }
    }
}
