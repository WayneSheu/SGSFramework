using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_RcaSend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SentPdfFileId",
                schema: "pms",
                table: "RcaReports",
                type: "uniqueidentifier",
                nullable: true,
                comment: "系統代寄時存檔的正式版 PDF（FMS 分類 pms-form，加密）；僅記錄模式為空");

            migrationBuilder.AddColumn<string>(
                name: "SentPdfFileName",
                schema: "pms",
                table: "RcaReports",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentTo",
                schema: "pms",
                table: "RcaReports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "系統代寄的收件人與副本摘要（僅 Email 模式；僅記錄模式為空）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentPdfFileId",
                schema: "pms",
                table: "RcaReports");

            migrationBuilder.DropColumn(
                name: "SentPdfFileName",
                schema: "pms",
                table: "RcaReports");

            migrationBuilder.DropColumn(
                name: "SentTo",
                schema: "pms",
                table: "RcaReports");
        }
    }
}
