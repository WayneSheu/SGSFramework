using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_UatSignoffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UatSignoffs",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, comment: "受驗收的版本號（vX.Y.Z）"),
                    ScopeNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "驗收範圍說明（納入驗收的功能與測試情境）"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, comment: "驗收內容雜湊（專案代號、版本、驗收範圍的 SHA-256）；簽核須對同一雜湊"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "驗收結果記錄時間"),
                    ResultByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "記錄結果的人（客戶外部帳號開放前為代錄的 PM）"),
                    ResultIsProxy = table.Column<bool>(type: "bit", nullable: false, comment: "是否由 PM 代錄客戶驗收結果"),
                    ProxyReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "代錄原因與依據（例如客戶 Email 回覆確認）"),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "客戶驗收意見／不通過原因／取消原因"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UatSignoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UatSignoffs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "UAT 驗收簽核單：版本、驗收範圍、內容雜湊與客戶驗收結果（目前由 PM 代錄）");

            migrationBuilder.CreateIndex(
                name: "IX_UatSignoffs_ProjectId_Status",
                schema: "pms",
                table: "UatSignoffs",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UatSignoffs_ProjectId_Version",
                schema: "pms",
                table: "UatSignoffs",
                columns: new[] { "ProjectId", "Version" },
                unique: true,
                filter: "[Status] <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UatSignoffs",
                schema: "pms");
        }
    }
}
