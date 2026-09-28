using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class user_lab_globalPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_User_Lab_Permissions",
                schema: "core",
                table: "User_Lab_Permissions");

            migrationBuilder.DropIndex(
                name: "ix_user_lab_permissions_user_lab_controller",
                schema: "core",
                table: "User_Lab_Permissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_User_Global_Permissions",
                schema: "core",
                table: "User_Global_Permissions");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "core",
                table: "User_Lab_Permissions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "core",
                table: "User_Lab_Permissions");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "core",
                table: "User_Global_Permissions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "core",
                table: "User_Global_Permissions");

            migrationBuilder.RenameTable(
                name: "User_Lab_Permissions",
                schema: "core",
                newName: "UserLabPermissions",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "User_Global_Permissions",
                schema: "core",
                newName: "UserGlobalPermissions",
                newSchema: "core");

            migrationBuilder.RenameColumn(
                name: "bitmask",
                schema: "core",
                table: "UserLabPermissions",
                newName: "Bitmask");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "core",
                table: "UserLabPermissions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                schema: "core",
                table: "UserLabPermissions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "tenant_lab_id",
                schema: "core",
                table: "UserLabPermissions",
                newName: "TenantLabId");

            migrationBuilder.RenameColumn(
                name: "controller_or_module_key",
                schema: "core",
                table: "UserLabPermissions",
                newName: "ControllerOrModuleKey");

            migrationBuilder.RenameIndex(
                name: "ix_user_lab_permissions_user_tenant",
                schema: "core",
                table: "UserLabPermissions",
                newName: "IX_UserLabPermissions_UserId_TenantLabId");

            migrationBuilder.RenameColumn(
                name: "bitmask",
                schema: "core",
                table: "UserGlobalPermissions",
                newName: "Bitmask");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "core",
                table: "UserGlobalPermissions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                schema: "core",
                table: "UserGlobalPermissions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "permission_key",
                schema: "core",
                table: "UserGlobalPermissions",
                newName: "PermissionKey");

            migrationBuilder.RenameIndex(
                name: "ix_user_global_permissions_user_key",
                schema: "core",
                table: "UserGlobalPermissions",
                newName: "UX_UserGlobalPermissions_UserId_PermissionKey");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserLabPermissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserLabPermissions",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserLabPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LabId",
                schema: "core",
                table: "UserLabPermissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserLabPermissions",
                type: "datetimeoffset(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "UserLabPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "datetimeoffset(7)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "UserGlobalPermissions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserLabPermissions",
                schema: "core",
                table: "UserLabPermissions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserGlobalPermissions",
                schema: "core",
                table: "UserGlobalPermissions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "UX_UserLabPermissions_User_Lab_Module",
                schema: "core",
                table: "UserLabPermissions",
                columns: new[] { "UserId", "LabId", "ControllerOrModuleKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserLabPermissions",
                schema: "core",
                table: "UserLabPermissions");

            migrationBuilder.DropIndex(
                name: "UX_UserLabPermissions_User_Lab_Module",
                schema: "core",
                table: "UserLabPermissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserGlobalPermissions",
                schema: "core",
                table: "UserGlobalPermissions");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "core",
                table: "UserLabPermissions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "core",
                table: "UserLabPermissions");

            migrationBuilder.DropColumn(
                name: "LabId",
                schema: "core",
                table: "UserLabPermissions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "UserLabPermissions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "core",
                table: "UserLabPermissions");

            migrationBuilder.RenameTable(
                name: "UserLabPermissions",
                schema: "core",
                newName: "User_Lab_Permissions",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "UserGlobalPermissions",
                schema: "core",
                newName: "User_Global_Permissions",
                newSchema: "core");

            migrationBuilder.RenameColumn(
                name: "Bitmask",
                schema: "core",
                table: "User_Lab_Permissions",
                newName: "bitmask");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "core",
                table: "User_Lab_Permissions",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "core",
                table: "User_Lab_Permissions",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "TenantLabId",
                schema: "core",
                table: "User_Lab_Permissions",
                newName: "tenant_lab_id");

            migrationBuilder.RenameColumn(
                name: "ControllerOrModuleKey",
                schema: "core",
                table: "User_Lab_Permissions",
                newName: "controller_or_module_key");

            migrationBuilder.RenameIndex(
                name: "IX_UserLabPermissions_UserId_TenantLabId",
                schema: "core",
                table: "User_Lab_Permissions",
                newName: "ix_user_lab_permissions_user_tenant");

            migrationBuilder.RenameColumn(
                name: "Bitmask",
                schema: "core",
                table: "User_Global_Permissions",
                newName: "bitmask");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "core",
                table: "User_Global_Permissions",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "core",
                table: "User_Global_Permissions",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "PermissionKey",
                schema: "core",
                table: "User_Global_Permissions",
                newName: "permission_key");

            migrationBuilder.RenameIndex(
                name: "UX_UserGlobalPermissions_UserId_PermissionKey",
                schema: "core",
                table: "User_Global_Permissions",
                newName: "ix_user_global_permissions_user_key");

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                schema: "core",
                table: "User_Lab_Permissions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "core",
                table: "User_Lab_Permissions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "core",
                table: "User_Lab_Permissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "core",
                table: "User_Global_Permissions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "core",
                table: "User_Global_Permissions",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "core",
                table: "User_Global_Permissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "core",
                table: "User_Global_Permissions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                schema: "core",
                table: "User_Global_Permissions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "core",
                table: "User_Global_Permissions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "core",
                table: "User_Global_Permissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_User_Lab_Permissions",
                schema: "core",
                table: "User_Lab_Permissions",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_User_Global_Permissions",
                schema: "core",
                table: "User_Global_Permissions",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_user_lab_permissions_user_lab_controller",
                schema: "core",
                table: "User_Lab_Permissions",
                columns: new[] { "user_id", "tenant_lab_id", "controller_or_module_key" },
                unique: true);
        }
    }
}
