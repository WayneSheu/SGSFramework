using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class Update_ControllerMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControllerIcon",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ControllerOrder",
                schema: "core",
                table: "ControllerMetadatas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsMenu",
                schema: "core",
                table: "ControllerMetadatas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metadata_IsMenu",
                schema: "core",
                table: "ControllerMetadatas",
                column: "IsMenu");

            migrationBuilder.CreateIndex(
                name: "IX_Metadata_IsMenu_ModuleName",
                schema: "core",
                table: "ControllerMetadatas",
                columns: new[] { "IsMenu", "ModuleName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Metadata_IsMenu",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropIndex(
                name: "IX_Metadata_IsMenu_ModuleName",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "ControllerIcon",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "ControllerOrder",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "IsMenu",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.DropColumn(
                name: "Path",
                schema: "core",
                table: "ControllerMetadatas");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionKey",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                schema: "core",
                table: "ControllerMetadatas",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
