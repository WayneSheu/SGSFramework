using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractNoticeSignoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Closing",
                schema: "pms",
                table: "ContractExtensionNotices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "寄出時的結語敬語（例：順頌商祺；可由 PM 編輯）");

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                schema: "pms",
                table: "ContractExtensionNotices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "寄出時的署名（公司、職稱、姓名；可由 PM 編輯）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Closing",
                schema: "pms",
                table: "ContractExtensionNotices");

            migrationBuilder.DropColumn(
                name: "Signature",
                schema: "pms",
                table: "ContractExtensionNotices");
        }
    }
}
