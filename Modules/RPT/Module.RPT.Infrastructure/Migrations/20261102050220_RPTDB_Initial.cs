using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.RPT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RPTDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rpt");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "rpt",
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
                name: "OutboxMessages",
                schema: "rpt",
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

            // 存取紀錄為 SQL Server Ledger 僅附加資料表：任何人（含 DBA）皆無法 UPDATE / DELETE（ISO 17025 稽核軌跡）
            migrationBuilder.Sql(@"
                CREATE TABLE [rpt].[ReportAccessLogs] (
                    [Id] uniqueidentifier NOT NULL,
                    [ReportId] uniqueidentifier NULL,
                    [ReportNo] nvarchar(40) NULL,
                    [ReportTypeCode] nvarchar(64) NOT NULL,
                    [Action] nvarchar(20) NOT NULL,
                    [UserId] nvarchar(64) NULL,
                    [UserName] nvarchar(256) NOT NULL,
                    [RemoteIp] nvarchar(64) NULL,
                    [UserAgent] nvarchar(512) NULL,
                    [Detail] nvarchar(500) NULL,
                    [OccurredAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_ReportAccessLogs] PRIMARY KEY ([Id])
                ) WITH (LEDGER = ON (APPEND_ONLY = ON));
            ");

            migrationBuilder.CreateTable(
                name: "Reports",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    ReportNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, comment: "報告編號（唯一；重發版本加 -R{n}）"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "版本（首版 1，每次重發 +1）"),
                    SupersedesReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "取代之前一版報告識別鍵（重發時）"),
                    CategoryCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "報告類別代碼（core.ReportCategories.Code）"),
                    TenantLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "所屬區域實驗室（分公司，ORG Organization.TenantLabId）"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "報告標題"),
                    CustomerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "客戶代碼"),
                    CustomerName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "客戶名稱（落地加密；盲樣時對檢驗人員遮蔽）"),
                    SampleNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "樣品編號（盲樣時檢驗人員僅能看到此欄）"),
                    SampleDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "樣品描述（盲樣時遮蔽）"),
                    Confidentiality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "法規密級（Normal=常規, Confidential=機密, Restricted=特檢）"),
                    IsBlind = table.Column<bool>(type: "bit", nullable: false, comment: "是否為盲樣（隱藏客戶與來源資訊）"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "生命週期狀態（Draft, InReview, Reviewed, Published, Voided）"),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "作者（數據登錄者）識別鍵"),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "作者名稱"),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "送審時間"),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "審核者識別鍵"),
                    ReviewerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "審核者名稱"),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "審核完成時間"),
                    SignerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "簽署人識別鍵"),
                    SignerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "簽署人名稱"),
                    SignerCertificateNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "簽署人資格證號（簽署當下快照）"),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "簽署 / 發行時間"),
                    PdfFileLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "正式報告 PDF 儲存連結"),
                    PdfSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "正式報告 PDF 之 SHA-256 雜湊（小寫十六進位）"),
                    VerificationToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "公開真偽查驗代碼"),
                    VoidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "作廢時間"),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "作廢原因"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後更新時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後更新人員"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false, comment: "並行版本（樂觀鎖）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                },
                comment: "檢驗報告：依類別與區域實驗室隔離，生命週期 Draft → InReview → Reviewed → Published → Voided；發行後唯讀，修正須作廢重發");

            migrationBuilder.CreateTable(
                name: "ReportStatusHistories",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "報告識別鍵"),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, comment: "原狀態（建立時為 null）"),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "新狀態"),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "動作（Create, Submit, ApproveReview, RejectReview, Sign, RejectSign, Void, Reissue）"),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "操作者識別鍵"),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "操作者名稱"),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "原因 / 意見"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "發生時間")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportStatusHistories", x => x.Id);
                },
                comment: "報告狀態轉換軌跡（只增不改）");

            migrationBuilder.CreateTable(
                name: "ReportVoidRequests",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "報告識別鍵"),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, comment: "作廢原因"),
                    Reissue = table.Column<bool>(type: "bit", nullable: false, comment: "核准後是否自動建立重發草稿"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "申請狀態（Pending, Approved, Rejected, Cancelled）"),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "申請人識別鍵"),
                    RequestedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "申請人名稱"),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "申請時間"),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "審核人識別鍵"),
                    ReviewedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "審核人名稱"),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "審核時間"),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "審核意見"),
                    ReissuedReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "重發之新版報告識別鍵"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後更新時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後更新人員")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportVoidRequests", x => x.Id);
                },
                comment: "報告作廢 / 重發申請（Maker-Checker：申請人不得核准）");

            migrationBuilder.CreateTable(
                name: "ReportAssignments",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "報告識別鍵"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "使用者識別鍵"),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "使用者名稱"),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "指派角色（Analyst, Reviewer, Signer, ProjectMember, SalesOwner）"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後更新時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後更新人員")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportAssignments_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "rpt",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "報告指派：被指派者於「僅經手」範圍下可存取此報告；特檢報告僅限指派人員");

            migrationBuilder.CreateTable(
                name: "ReportResults",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "報告識別鍵"),
                    Sequence = table.Column<int>(type: "int", nullable: false, comment: "排序"),
                    ItemCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "檢驗項目代碼"),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "檢驗項目名稱"),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "檢驗結果值"),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true, comment: "單位"),
                    LimitText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "法規限值 / 規格"),
                    Method = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "檢驗方法"),
                    ValueSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "數據來源（Manual=人工, Instrument=儀器拋轉）"),
                    InstrumentValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "儀器拋轉之原始值（人工修改後仍保留）"),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "變更原因（儀器數據人工修改時必填）"),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "備註"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "建立時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "建立人員"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最後更新時間"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "最後更新人員")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportResults_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "rpt",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "檢驗結果數據列；修改由稽核攔截器保留原值、新值與修改人，儀器拋轉數據人工修改須填寫變更原因");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_OutboxMessages_CorrelationId",
                schema: "rpt",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_OutboxMessages_FetchPending",
                schema: "rpt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "IsDead", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportAccessLogs_ReportId_OccurredAt",
                schema: "rpt",
                table: "ReportAccessLogs",
                columns: new[] { "ReportId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportAssignments_ReportId_UserId_Role",
                schema: "rpt",
                table: "ReportAssignments",
                columns: new[] { "ReportId", "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportAssignments_UserId",
                schema: "rpt",
                table: "ReportAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportResults_ReportId_Sequence",
                schema: "rpt",
                table: "ReportResults",
                columns: new[] { "ReportId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportNo",
                schema: "rpt",
                table: "Reports",
                column: "ReportNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_SampleNo",
                schema: "rpt",
                table: "Reports",
                column: "SampleNo");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TenantLabId_CategoryCode_Status",
                schema: "rpt",
                table: "Reports",
                columns: new[] { "TenantLabId", "CategoryCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_VerificationToken",
                schema: "rpt",
                table: "Reports",
                column: "VerificationToken",
                unique: true,
                filter: "[VerificationToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportStatusHistories_ReportId_OccurredAt",
                schema: "rpt",
                table: "ReportStatusHistories",
                columns: new[] { "ReportId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportVoidRequests_ReportId_Status",
                schema: "rpt",
                table: "ReportVoidRequests",
                columns: new[] { "ReportId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "ReportAccessLogs",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "ReportAssignments",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "ReportResults",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "ReportStatusHistories",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "ReportVoidRequests",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "Reports",
                schema: "rpt");
        }
    }
}
