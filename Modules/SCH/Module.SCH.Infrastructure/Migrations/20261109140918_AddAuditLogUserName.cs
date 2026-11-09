using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.SCH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserName",
                schema: "sch",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "人員姓名");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserName",
                schema: "sch",
                table: "AuditLogs");
        }
    }
}
