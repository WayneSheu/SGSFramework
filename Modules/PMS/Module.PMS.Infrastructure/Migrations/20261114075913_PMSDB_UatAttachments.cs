using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_UatAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UatAttachments",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UatSignoffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "FMS 檔案識別碼"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false, comment: "檔名（系統產生：YYYYMMDD_專案代號_UAT_版本_檔案說明）"),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    MaskConfirmed = table.Column<bool>(type: "bit", nullable: false, comment: "已確認遮罩個資／密碼／金鑰（SOP 8-4）"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UatAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UatAttachments_UatSignoffs_UatSignoffId",
                        column: x => x.UatSignoffId,
                        principalSchema: "pms",
                        principalTable: "UatSignoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "UAT 驗收簽核單附件索引：實體檔案儲存於 FMS，僅記錄 FileId");

            migrationBuilder.CreateIndex(
                name: "IX_UatAttachments_UatSignoffId_IsDeleted",
                schema: "pms",
                table: "UatAttachments",
                columns: new[] { "UatSignoffId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UatAttachments",
                schema: "pms");
        }
    }
}
