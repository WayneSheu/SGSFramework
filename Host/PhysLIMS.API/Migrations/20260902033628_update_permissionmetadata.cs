using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_permissionmetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ControllerTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ModuleTitle",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NodePath",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionMetadata_ParentId",
                schema: "core",
                table: "PermissionMetadata",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PermissionMetadata_PermissionMetadata_ParentId",
                schema: "core",
                table: "PermissionMetadata",
                column: "ParentId",
                principalSchema: "core",
                principalTable: "PermissionMetadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PermissionMetadata_PermissionMetadata_ParentId",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.DropIndex(
                name: "IX_PermissionMetadata_ParentId",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.DropColumn(
                name: "ControllerTitle",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.DropColumn(
                name: "ModuleTitle",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.DropColumn(
                name: "NodePath",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.AlterColumn<string>(
                name: "ModuleName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ControllerName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);
        }
    }
}
