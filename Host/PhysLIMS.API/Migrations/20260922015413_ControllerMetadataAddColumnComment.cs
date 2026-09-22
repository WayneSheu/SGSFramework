using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class ControllerMetadataAddColumnComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Version",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: false,
                comment: "API 版本號 (例如: v1, v2, etc.)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RouteTemplate",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                comment: "供前端 Axios / HttpClient 進行 HTTP 請求。",
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "權限Key (供前端 Axios / HttpClient 顯示)。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "供前端路由系統進行頁面跳轉與選單點擊導航。",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ParentMenuName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: true,
                comment: "父級選單名稱 (供前端 Axios / HttpClient 顯示)。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleTitle",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "取自AssemblyInfo Attribute 的ModulTitle。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "取自AssemblyInfo Attribute 的ModuleName 屬性。",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "IsMenu",
                schema: "core",
                table: "ControllerMetadatas",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否為選單 (由 [Function] Attribute 之 IsMenu 標記)。",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "ControllerMetadatas",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "標記是否啟用",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "動作圖示 (供前端 Axios / HttpClient 顯示)。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DisplayOrder",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: false,
                comment: "動作排序 (供前端 Axios / HttpClient 顯示)。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "動作標題 (供前端 Axios / HttpClient 顯示)。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: true,
                comment: "動作說明 (供前端 Axios / HttpClient 顯示)。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "core",
                table: "ControllerMetadatas",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                comment: "建立日期",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerTypeName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: false,
                comment: "取自ControllerTypeName Atteribute 的ControllerTypeName。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerTitle",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "取自ControllerTitle Atteribute 的ControllerTitle。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "ControllerOrder",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: false,
                comment: "取自ControllerOrder Atteribute 的ControllerOrder。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "取自ControllerName Atteribute 的ControllerName。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ControllerIcon",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "取自ControllerIcon Atteribute 的ControllerIcon。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BitPosition",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: true,
                comment: "該 Action 在模組內對應的位元位置 (0 ~ 63)，用於 Bitmask 快速運算。",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AttributesJson",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: true,
                comment: " Controller 或 Action 完整 Attributes 集合 (JSON 格式)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActionName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(450)",
                nullable: false,
                comment: "取自ActionName Atteribute 的ActionName。",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Version",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "API 版本號 (例如: v1, v2, etc.)");

            migrationBuilder.AlterColumn<string>(
                name: "RouteTemplate",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldComment: "供前端 Axios / HttpClient 進行 HTTP 請求。");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "權限Key (供前端 Axios / HttpClient 顯示)。");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "供前端路由系統進行頁面跳轉與選單點擊導航。");

            migrationBuilder.AlterColumn<string>(
                name: "ParentMenuName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "父級選單名稱 (供前端 Axios / HttpClient 顯示)。");

            migrationBuilder.AlterColumn<string>(
                name: "ModuleTitle",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "取自AssemblyInfo Attribute 的ModulTitle。");

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldComment: "取自AssemblyInfo Attribute 的ModuleName 屬性。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsMenu",
                schema: "core",
                table: "ControllerMetadatas",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false,
                oldComment: "是否為選單 (由 [Function] Attribute 之 IsMenu 標記)。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "ControllerMetadatas",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true,
                oldComment: "標記是否啟用");

            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "動作圖示 (供前端 Axios / HttpClient 顯示)。");

            migrationBuilder.AlterColumn<int>(
                name: "DisplayOrder",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "動作排序 (供前端 Axios / HttpClient 顯示)。");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "動作標題 (供前端 Axios / HttpClient 顯示)。");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "動作說明 (供前端 Axios / HttpClient 顯示)。");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "core",
                table: "ControllerMetadatas",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()",
                oldComment: "建立日期");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerTypeName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "取自ControllerTypeName Atteribute 的ControllerTypeName。");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerTitle",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "取自ControllerTitle Atteribute 的ControllerTitle。");

            migrationBuilder.AlterColumn<int>(
                name: "ControllerOrder",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "取自ControllerOrder Atteribute 的ControllerOrder。");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "取自ControllerName Atteribute 的ControllerName。");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerIcon",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "取自ControllerIcon Atteribute 的ControllerIcon。");

            migrationBuilder.AlterColumn<int>(
                name: "BitPosition",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComment: "該 Action 在模組內對應的位元位置 (0 ~ 63)，用於 Bitmask 快速運算。");

            migrationBuilder.AlterColumn<string>(
                name: "AttributesJson",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: " Controller 或 Action 完整 Attributes 集合 (JSON 格式)");

            migrationBuilder.AlterColumn<string>(
                name: "ActionName",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "取自ActionName Atteribute 的ActionName。");
        }
    }
}
