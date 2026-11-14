using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_DeliverableFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "pms",
                table: "Deliverables",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: false,
                comment: "檔名（系統依命名鐵律產生）",
                oldClrType: typeof(string),
                oldType: "nvarchar(260)",
                oldMaxLength: 260,
                oldComment: "檔名（已通過命名鐵律檢查）");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                schema: "pms",
                table: "Deliverables",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "內容類型");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                schema: "pms",
                table: "Deliverables",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "pms",
                table: "Deliverables",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocName",
                schema: "pms",
                table: "Deliverables",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "文件名稱（命名用，如：系統需求規格書）");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                schema: "pms",
                table: "Deliverables",
                type: "bigint",
                nullable: true,
                comment: "檔案大小（位元組）；NULL 表示 FMS 無實檔（舊版僅登記索引）");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "pms",
                table: "Deliverables",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否已刪除（虛擬刪除；FMS 實體檔案於保留期滿後由排程清除）");

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                schema: "pms",
                table: "Deliverables",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true,
                comment: "使用者上傳時的原始檔名");

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                schema: "pms",
                table: "Deliverables",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true,
                comment: "內容 SHA-256（十六進位）");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_ProjectId_ModuleId_DocType_Version",
                schema: "pms",
                table: "Deliverables",
                columns: new[] { "ProjectId", "ModuleId", "DocType", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Deliverables_ProjectId_ModuleId_DocType_Version",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "ContentType",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "DocName",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "FileSize",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "Sha256",
                schema: "pms",
                table: "Deliverables");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "pms",
                table: "Deliverables",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: false,
                comment: "檔名（已通過命名鐵律檢查）",
                oldClrType: typeof(string),
                oldType: "nvarchar(260)",
                oldMaxLength: 260,
                oldComment: "檔名（系統依命名鐵律產生）");
        }
    }
}
