using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class update_systemlog_labname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantName",
                schema: "core",
                table: "SystemLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "實驗室名稱 (TenantName)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantName",
                schema: "core",
                table: "SystemLogs");
        }
    }
}
