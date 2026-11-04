using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.FMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FMSDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fms");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "fms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "稽核日誌識別主鍵")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TraceId = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "使用者識別"),
                    RemoteIp = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "遠端 IP 位址"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "時間戳記"),
                    Schema = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "資料表綱要"),
                    TableName = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "資料表名稱"),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "操作類型"),
                    KeyValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "主鍵值"),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "舊值"),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "新值"),
                    ChangedColumns = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "變更欄位"),
                    PreviousHash = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "前一筆雜湊值"),
                    StoredHash = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "儲存雜湊值"),
                    IsRepaired = table.Column<bool>(type: "bit", nullable: false, comment: "是否已修復"),
                    RepairedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "修復時間"),
                    GapReason = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "差異原因"),
                    OriginalStoredHash = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "原始儲存雜湊值")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                },
                comment: "資料稽核日誌實體");

            migrationBuilder.CreateTable(
                name: "FileMetadatas",
                schema: "fms",
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

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "fms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    CorrelationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true, comment: "關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)"),
                    CausationId = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true, comment: "誘發 ID：代表觸發此事件的具體 Command 名稱或 ID"),
                    Type = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false, comment: "事件的完整型別名稱 (包含 Namespace)，用於還原物件"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "事件序列化後的 JSON 內容"),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "事件發生的原始時間 (UTC)"),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。"),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。"),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "目前已重試的次數"),
                    IsDead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。"),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                },
                comment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_Category_CreatedAtUtc",
                schema: "fms",
                table: "FileMetadatas",
                columns: new[] { "Category", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_IsDeleted_PurgedAtUtc_DeletedAtUtc",
                schema: "fms",
                table: "FileMetadatas",
                columns: new[] { "IsDeleted", "PurgedAtUtc", "DeletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_ReferenceType_ReferenceId",
                schema: "fms",
                table: "FileMetadatas",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_StorageTarget_ContainerName_StoredKey",
                schema: "fms",
                table: "FileMetadatas",
                columns: new[] { "StorageTarget", "ContainerName", "StoredKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fms_OutboxMessages_CorrelationId",
                schema: "fms",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_fms_OutboxMessages_FetchPending",
                schema: "fms",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "IsDead", "ScheduledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "fms");

            migrationBuilder.DropTable(
                name: "FileMetadatas",
                schema: "fms");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "fms");
        }
    }
}
