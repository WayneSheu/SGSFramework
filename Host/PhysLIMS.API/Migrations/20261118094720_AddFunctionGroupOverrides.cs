using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionGroupOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupControllerName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "顯示用改掛：功能顯示於哪個 Controller 之下；為 null 時顯示於原 Controller");

            migrationBuilder.AddColumn<string>(
                name: "GroupModuleName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "顯示用改掛：功能顯示於哪個 Controller 之下所屬的模組；僅影響畫面分組");

            migrationBuilder.CreateTable(
                name: "FunctionGroupOverrides",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    Level = table.Column<int>(type: "int", nullable: false, comment: "分組層級：1 = 模組、2 = Controller"),
                    ModuleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "來源模組名稱（ControllerMetadata.ModuleName）"),
                    ControllerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "來源 Controller 名稱；模組層級為空字串"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "自訂顯示名稱；為 null 時沿用程式碼標題"),
                    Icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "自訂圖示 (Font Awesome class)；僅 Controller 層級使用"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true, comment: "自訂排序；為 null 時沿用預設排序"),
                    TargetModuleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "Controller 層級：改掛顯示於哪個模組之下；為 null 時顯示於原模組"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後異動者"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "最後異動時間 (UTC)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionGroupOverrides", x => x.Id);
                },
                comment: "系統功能管理：模組 / Controller 層級的顯示覆寫（名稱、圖示、排序、顯示用父層）");

            migrationBuilder.CreateIndex(
                name: "UX_FunctionGroupOverride_Level_Module_Controller",
                schema: "core",
                table: "FunctionGroupOverrides",
                columns: new[] { "Level", "ModuleName", "ControllerName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FunctionGroupOverrides",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "GroupControllerName",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "GroupModuleName",
                schema: "core",
                table: "ControllerMetadatas");
        }
    }
}
