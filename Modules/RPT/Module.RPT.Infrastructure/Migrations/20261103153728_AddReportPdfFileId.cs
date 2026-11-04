using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.RPT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportPdfFileId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PdfFileLink",
                schema: "rpt",
                table: "Reports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "舊版正式報告 PDF 儲存連結（LocalFileStorageHelper），僅供改接檔案管理前已簽署的報告讀取",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "正式報告 PDF 儲存連結");

            migrationBuilder.AddColumn<Guid>(
                name: "PdfFileId",
                schema: "rpt",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true,
                comment: "正式報告 PDF 於檔案管理 (core.FileMetadatas) 的檔案識別碼");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_PdfFileId",
                schema: "rpt",
                table: "Reports",
                column: "PdfFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_PdfFileId",
                schema: "rpt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "PdfFileId",
                schema: "rpt",
                table: "Reports");

            migrationBuilder.AlterColumn<string>(
                name: "PdfFileLink",
                schema: "rpt",
                table: "Reports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "正式報告 PDF 儲存連結",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "舊版正式報告 PDF 儲存連結（LocalFileStorageHelper），僅供改接檔案管理前已簽署的報告讀取");
        }
    }
}
