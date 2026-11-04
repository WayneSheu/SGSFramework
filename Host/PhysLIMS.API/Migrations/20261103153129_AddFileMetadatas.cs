using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysLIMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFileMetadatas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileMetadatas",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵（同時作為實體檔名）"),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, comment: "使用者上傳時的原始檔名（僅供顯示與下載，不作為儲存路徑）"),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "經檔案簽章 (Magic Number) 驗證後的安全副檔名（小寫，含句點）"),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "依檔案簽章判定的 MIME 類型（不採信用戶端 Content-Type）"),
                    FileSize = table.Column<long>(type: "bigint", nullable: false, comment: "實際儲存的檔案大小（位元組，圖片為重新編碼後大小）"),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "實際儲存內容的 SHA-256（十六進位小寫），作為 ETag 與完整性查驗"),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "業務分類（對應 FileManagement:Categories，決定儲存目標與允許格式）"),
                    StorageTarget = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "儲存目標：LocalPrivate（地端機密）/ CloudPublic（公開資源）"),
                    ContainerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "儲存容器（檔案系統為子目錄，物件儲存為 Bucket / Container）"),
                    StoredKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "容器內的實體儲存鍵（yyyy/MM/{Id}{副檔名}），由系統產生"),
                    ReferenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "關聯的業務實體類型（例如 Report）；未關聯為 null"),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "關聯的業務實體識別碼；未關聯為 null"),
                    ImageWidth = table.Column<int>(type: "int", nullable: true, comment: "圖片寬度（像素）；非圖片為 null"),
                    ImageHeight = table.Column<int>(type: "int", nullable: true, comment: "圖片高度（像素）；非圖片為 null"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, comment: "虛擬刪除旗標；實體檔案於保留期滿後由排程清除"),
                    DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "執行刪除的人員"),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "虛擬刪除時間"),
                    PurgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "實體檔案自儲存體清除的時間；null 表示實體檔案仍存在"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "上傳人員"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "上傳時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後異動人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後異動時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMetadatas", x => x.Id);
                },
                comment: "檔案中繼資料：業務屬性與實體儲存屬性分離，刪除為虛擬刪除，實體檔案由排程批次清除");

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_Category_CreatedAtUtc",
                schema: "core",
                table: "FileMetadatas",
                columns: new[] { "Category", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_IsDeleted_PurgedAtUtc_DeletedAtUtc",
                schema: "core",
                table: "FileMetadatas",
                columns: new[] { "IsDeleted", "PurgedAtUtc", "DeletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_ReferenceType_ReferenceId",
                schema: "core",
                table: "FileMetadatas",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_StorageTarget_ContainerName_StoredKey",
                schema: "core",
                table: "FileMetadatas",
                columns: new[] { "StorageTarget", "ContainerName", "StoredKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileMetadatas",
                schema: "core");
        }
    }
}
