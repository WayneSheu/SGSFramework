using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.RPT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class update_report_pdffileid_comment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PdfFileId",
                schema: "rpt",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true,
                comment: "正式報告 PDF 於檔案管理模組 (fms.FileMetadatas) 的檔案識別碼",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "正式報告 PDF 於檔案管理 (core.FileMetadatas) 的檔案識別碼");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PdfFileId",
                schema: "rpt",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true,
                comment: "正式報告 PDF 於檔案管理 (core.FileMetadatas) 的檔案識別碼",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "正式報告 PDF 於檔案管理模組 (fms.FileMetadatas) 的檔案識別碼");
        }
    }
}
