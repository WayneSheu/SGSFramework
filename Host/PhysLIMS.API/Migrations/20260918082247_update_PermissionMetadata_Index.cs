using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_PermissionMetadata_Index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PermissionMetadata_BitPosition",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.DropIndex(
                name: "IX_PermissionMetadata_PermissionKey",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.AlterColumn<int>(
                name: "ParentId",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true)
                .Annotation("Relational:ColumnOrder", 11);

            migrationBuilder.AlterColumn<string>(
                name: "NodePath",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)")
                .Annotation("Relational:ColumnOrder", 12);

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:ColumnOrder", 13);

            migrationBuilder.AlterColumn<int>(
                name: "BitPosition",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:ColumnOrder", 10)
                .OldAnnotation("Relational:ColumnOrder", 9);

            migrationBuilder.AlterColumn<string>(
                name: "ActionName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionMetadata_PermissionKey_ControllerName_ActionName",
                schema: "core",
                table: "PermissionMetadata",
                columns: new[] { "PermissionKey", "ControllerName", "ActionName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PermissionMetadata_PermissionKey_ControllerName_ActionName",
                schema: "core",
                table: "PermissionMetadata");

            migrationBuilder.AlterColumn<int>(
                name: "ParentId",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: true,
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
                oldClrType: typeof(string),
                oldType: "nvarchar(max)")
                .OldAnnotation("Relational:ColumnOrder", 12);

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("Relational:ColumnOrder", 13);

            migrationBuilder.AlterColumn<int>(
                name: "BitPosition",
                schema: "core",
                table: "PermissionMetadata",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:ColumnOrder", 9)
                .OldAnnotation("Relational:ColumnOrder", 10);

            migrationBuilder.AlterColumn<string>(
                name: "ActionName",
                schema: "core",
                table: "PermissionMetadata",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionMetadata_BitPosition",
                schema: "core",
                table: "PermissionMetadata",
                column: "BitPosition",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionMetadata_PermissionKey",
                schema: "core",
                table: "PermissionMetadata",
                column: "PermissionKey",
                unique: true);
        }
    }
}
