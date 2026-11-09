using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.LOG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserName",
                schema: "log",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "人員姓名");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserName",
                schema: "log",
                table: "AuditLogs");
        }
    }
}
