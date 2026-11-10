using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.FMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "fms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "fms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "稽核日誌識別主鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "操作類型"),
                    ChangedColumns = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "變更欄位"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    GapReason = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "差異原因"),
                    IsRepaired = table.Column<bool>(type: "bit", nullable: false, comment: "是否已修復"),
                    KeyValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "主鍵值"),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "新值"),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "舊值"),
                    OriginalStoredHash = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "原始儲存雜湊值"),
                    PreviousHash = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "前一筆雜湊值"),
                    RemoteIp = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "遠端 IP 位址"),
                    RepairedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "修復時間"),
                    Schema = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "資料表綱要"),
                    StoredHash = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "儲存雜湊值"),
                    TableName = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "資料表名稱"),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "時間戳記"),
                    TraceId = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "使用者識別"),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "人員姓名")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                },
                comment: "資料稽核日誌實體");
        }
    }
}
