using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class UserLabMappingAssignmentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLabMappings_User_Active_Primary",
                schema: "core",
                table: "UserLabMappings");

            migrationBuilder.DropIndex(
                name: "UX_UserLabMappings_OnePrimaryPerUser",
                schema: "core",
                table: "UserLabMappings");

            migrationBuilder.AddColumn<int>(
                name: "AssignmentType",
                schema: "core",
                table: "UserLabMappings",
                type: "int",
                nullable: false,
                defaultValue: 1,
                comment: "歸屬類別：0=主要、1=兼任、2=支援");

            // 1) 以舊 IsPrimary 回填：主要=0、其餘(兼任)=1
            migrationBuilder.Sql(
                "UPDATE [core].[UserLabMappings] SET [AssignmentType] = CASE WHEN [IsPrimary] = 1 THEN 0 ELSE 1 END;");

            // 2) 移除暫時預設值（Primary=0 為 CLR 預設，不可由 DB 預設值覆蓋）
            migrationBuilder.AlterColumn<int>(
                name: "AssignmentType",
                schema: "core",
                table: "UserLabMappings",
                type: "int",
                nullable: false,
                comment: "歸屬類別：0=主要、1=兼任、2=支援",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1,
                oldComment: "歸屬類別：0=主要、1=兼任、2=支援");

            // 3) SQL Server 不能把一般欄位 ALTER 成計算欄位：先 DROP 再以計算欄位重建
            //    （vw_UserAccessibleLabs 未使用 SCHEMABINDING，欄位同名重建後自動恢復可用）
            migrationBuilder.DropColumn(
                name: "IsPrimary",
                schema: "core",
                table: "UserLabMappings");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                schema: "core",
                table: "UserLabMappings",
                type: "bit",
                nullable: false,
                computedColumnSql: "CASE WHEN [AssignmentType] = 0 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
                comment: "是否為主要實驗室（由 AssignmentType 計算，唯讀）");

            migrationBuilder.CreateIndex(
                name: "IX_UserLabMappings_User_Active_Primary",
                schema: "core",
                table: "UserLabMappings",
                columns: new[] { "UserId", "IsActive", "AssignmentType" });

            migrationBuilder.CreateIndex(
                name: "UX_UserLabMappings_OnePrimaryPerUser",
                schema: "core",
                table: "UserLabMappings",
                column: "UserId",
                unique: true,
                filter: "[AssignmentType] = 0 AND [IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLabMappings_User_Active_Primary",
                schema: "core",
                table: "UserLabMappings");

            migrationBuilder.DropIndex(
                name: "UX_UserLabMappings_OnePrimaryPerUser",
                schema: "core",
                table: "UserLabMappings");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                schema: "core",
                table: "UserLabMappings");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                schema: "core",
                table: "UserLabMappings",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否為主要實驗室");

            migrationBuilder.Sql(
                "UPDATE [core].[UserLabMappings] SET [IsPrimary] = CASE WHEN [AssignmentType] = 0 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "AssignmentType",
                schema: "core",
                table: "UserLabMappings");

            migrationBuilder.CreateIndex(
                name: "IX_UserLabMappings_User_Active_Primary",
                schema: "core",
                table: "UserLabMappings",
                columns: new[] { "UserId", "IsActive", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "UX_UserLabMappings_OnePrimaryPerUser",
                schema: "core",
                table: "UserLabMappings",
                column: "UserId",
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsActive] = 1");
        }
    }
}
