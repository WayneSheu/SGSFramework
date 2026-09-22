using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class PermissionMetadataAddColumnComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SectionName",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.AlterTable(
                name: "PermissionMetadata",
                schema: "core",
                comment: "權限中繼資料實體");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                comment: "權限標題，對應ControllerMetadatas 的PermissionTitle。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)")
                .OldAnnotation("Relational:ColumnOrder", 8);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "權限代碼，對應ControllerMetadatas 的PermissionKey。",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128)
                .OldAnnotation("Relational:ColumnOrder", 9);

            migrationBuilder.AlterColumn<int>(
                name: "ParentId",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: true,
                comment: "父節點ID",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true)
                .OldAnnotation("Relational:ColumnOrder", 11);

            migrationBuilder.AlterColumn<string>(
                name: "NodePath",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                comment: "物化路徑，例如：1/2/3/4/5/",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)")
                .OldAnnotation("Relational:ColumnOrder", 12);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "模組標題，對應ControllerMetadatas 的ModuleTitle，例如系統管理、組織管理。",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true)
                .OldAnnotation("Relational:ColumnOrder", 2);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "模組名稱，對應ControllerMetadatas 的ModuleName，例如SGSFramework.System、SGSFramework.System。",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128)
                .OldAnnotation("Relational:ColumnOrder", 1);

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                comment: "階層深度",
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("Relational:ColumnOrder", 13);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "操作說明，對應ControllerMetadatas 的Description。",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256)
                .OldAnnotation("Relational:ColumnOrder", 7);

            migrationBuilder.AlterColumn<string>(
                name: "ControllerTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "功能標題，對應ControllerMetadatas 的ControllerTitle。",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true)
                .OldAnnotation("Relational:ColumnOrder", 4);

            migrationBuilder.AlterColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "功能名稱，對應ControllerMetadatas 的ControllerName。",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128)
                .OldAnnotation("Relational:ColumnOrder", 3);

            migrationBuilder.AlterColumn<int>(
                name: "BitPosition",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                comment: "位址權限，對應ControllerMetadatas 的BitPosition。",
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("Relational:ColumnOrder", 10);

            migrationBuilder.AlterColumn<string>(
                name: "ActionTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                comment: "操作標題，對應ControllerMetadatas 的ActionTitle。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)")
                .OldAnnotation("Relational:ColumnOrder", 6);

            migrationBuilder.AlterColumn<string>(
                name: "ActionName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(450)",
                nullable: false,
                comment: "操作名稱，對應ControllerMetadatas 的ActionName。",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)")
                .OldAnnotation("Relational:ColumnOrder", 5);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                comment: "主鍵",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("Relational:ColumnOrder", 0)
                .OldAnnotation("SqlServer:Identity", "1, 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "PermissionMetadata",
                schema: "core",
                oldComment: "權限中繼資料實體");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "權限標題，對應ControllerMetadatas 的PermissionTitle。")
                .Annotation("Relational:ColumnOrder", 8);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "權限代碼，對應ControllerMetadatas 的PermissionKey。")
                .Annotation("Relational:ColumnOrder", 9);

            migrationBuilder.AlterColumn<int>(
                name: "ParentId",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComment: "父節點ID")
                .Annotation("Relational:ColumnOrder", 11);

            migrationBuilder.AlterColumn<string>(
                name: "NodePath",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "物化路徑，例如：1/2/3/4/5/")
                .Annotation("Relational:ColumnOrder", 12);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "模組標題，對應ControllerMetadatas 的ModuleTitle，例如系統管理、組織管理。")
                .Annotation("Relational:ColumnOrder", 2);

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "模組名稱，對應ControllerMetadatas 的ModuleName，例如SGSFramework.System、SGSFramework.System。")
                .Annotation("Relational:ColumnOrder", 1);

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "階層深度")
                .Annotation("Relational:ColumnOrder", 13);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "操作說明，對應ControllerMetadatas 的Description。")
                .Annotation("Relational:ColumnOrder", 7);

            migrationBuilder.AlterColumn<string>(
                name: "ControllerTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "功能標題，對應ControllerMetadatas 的ControllerTitle。")
                .Annotation("Relational:ColumnOrder", 4);

            migrationBuilder.AlterColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "功能名稱，對應ControllerMetadatas 的ControllerName。")
                .Annotation("Relational:ColumnOrder", 3);

            migrationBuilder.AlterColumn<int>(
                name: "BitPosition",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "位址權限，對應ControllerMetadatas 的BitPosition。")
                .Annotation("Relational:ColumnOrder", 10);

            migrationBuilder.AlterColumn<string>(
                name: "ActionTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "操作標題，對應ControllerMetadatas 的ActionTitle。")
                .Annotation("Relational:ColumnOrder", 6);

            migrationBuilder.AlterColumn<string>(
                name: "ActionName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldComment: "操作名稱，對應ControllerMetadatas 的ActionName。")
                .Annotation("Relational:ColumnOrder", 5);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "主鍵")
                .Annotation("Relational:ColumnOrder", 0)
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<string>(
                name: "SectionName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
