using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ContractNoticeParams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoopNoticeWorkdays",
                schema: "pms",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 3,
                comment: "甲方協力遲延後，乙方須發出書面通知的工作日數（預設 3；各協力事項可個別覆寫）");

            migrationBuilder.AddColumn<string>(
                name: "BodyText",
                schema: "pms",
                table: "ContractExtensionNotices",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                comment: "寄出時的內文（可由 PM 編輯；與 PDF／Email 內容一致，供稽核回溯）");

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                schema: "pms",
                table: "ContractExtensionNotices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "寄出時的主旨（可由 PM 編輯）");

            migrationBuilder.AddColumn<int>(
                name: "NoticeWorkdays",
                schema: "pms",
                table: "ContractCoopItems",
                type: "int",
                nullable: true,
                comment: "此事項的書面通知期限（遲延後幾個工作日內）；null＝沿用合約約定");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoopNoticeWorkdays",
                schema: "pms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "BodyText",
                schema: "pms",
                table: "ContractExtensionNotices");

            migrationBuilder.DropColumn(
                name: "Subject",
                schema: "pms",
                table: "ContractExtensionNotices");

            migrationBuilder.DropColumn(
                name: "NoticeWorkdays",
                schema: "pms",
                table: "ContractCoopItems");
        }
    }
}
