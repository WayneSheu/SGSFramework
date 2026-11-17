using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class report_category_scope_storage_security : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EncryptAtRest",
                schema: "core",
                table: "ReportCategories",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "正式報告落地加密（AES-256-GCM 信封加密）；啟用時即使儲存分類未設定加密亦強制加密");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerTenantLabId",
                schema: "core",
                table: "ReportCategories",
                type: "uniqueidentifier",
                nullable: true,
                comment: "實驗室專屬類別之所屬區域實驗室（ORG Organization.TenantLabId）；全域類別為 null");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                schema: "core",
                table: "ReportCategories",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Global",
                comment: "適用範圍（Global=全域, Lab=實驗室專屬）");

            migrationBuilder.AddColumn<string>(
                name: "StorageCategory",
                schema: "core",
                table: "ReportCategories",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "正式報告存放的檔案管理儲存分類（FileManagement:Categories，決定地端 / 雲端與容器）；null 使用預設 Report 分類");

            migrationBuilder.AddColumn<string>(
                name: "StorageFolder",
                schema: "core",
                table: "ReportCategories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "儲存資料夾（儲存鍵前綴，英數 / 底線 / 連字號以 / 分隔）；null 使用 reports/{類別代碼}");

            migrationBuilder.AddColumn<bool>(
                name: "TamperVerification",
                schema: "core",
                table: "ReportCategories",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "防竄改驗證：簽署時產生公開查驗碼、下載時比對 SHA-256 並開放公開真偽查驗");

            migrationBuilder.AddColumn<bool>(
                name: "WatermarkOnDownload",
                schema: "core",
                table: "ReportCategories",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "下載正式報告時提供點陣化浮水印版本（下載者 / 時間 / IP），原檔不外流");

            migrationBuilder.CreateIndex(
                name: "IX_ReportCategories_OwnerTenantLabId",
                schema: "core",
                table: "ReportCategories",
                column: "OwnerTenantLabId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportCategories_OwnerTenantLabId",
                schema: "core",
                table: "ReportCategories");

            migrationBuilder.DropColumn(
                name: "EncryptAtRest",
                schema: "core",
                table: "ReportCategories");

            migrationBuilder.DropColumn(
                name: "OwnerTenantLabId",
                schema: "core",
                table: "ReportCategories");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "core",
                table: "ReportCategories");

            migrationBuilder.DropColumn(
                name: "StorageCategory",
                schema: "core",
                table: "ReportCategories");

            migrationBuilder.DropColumn(
                name: "StorageFolder",
                schema: "core",
                table: "ReportCategories");

            migrationBuilder.DropColumn(
                name: "TamperVerification",
                schema: "core",
                table: "ReportCategories");

            migrationBuilder.DropColumn(
                name: "WatermarkOnDownload",
                schema: "core",
                table: "ReportCategories");
        }
    }
}
