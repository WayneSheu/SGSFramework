using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_Tickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "pms",
                columns: table => new
                {
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "日期（主鍵）"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsWorkday = table.Column<bool>(type: "bit", nullable: false, comment: "是否為工作日：false=放假（平日國定假日）；true=補班（週末上班）"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.HolidayDate);
                },
                comment: "國定假日與補班日：SLA 工作天計算使用");

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, comment: "單號（專案代號-流水號，例 PRJ-001）"),
                    Type = table.Column<byte>(type: "tinyint", nullable: false, comment: "類型：諮詢／CR／異常／資料撈取"),
                    Priority = table.Column<byte>(type: "tinyint", nullable: true, comment: "優先級（僅系統異常）"),
                    Stage = table.Column<byte>(type: "tinyint", nullable: false, comment: "受理時專案所處期別（UAT／SLA）"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "問題描述（敏感，列表不輸出）"),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "發生環境（正式／UAT）"),
                    Impact = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "影響程度（完全無法作業／有替代方式）"),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    CustomerVisible = table.Column<bool>(type: "bit", nullable: false, comment: "客戶可見：Stakeholder 僅得見自己提出或標示可見者"),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "提出人（建單者）使用者識別碼"),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FirstResponseAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "首次回應時間（分流時寫入）"),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "應解決期限（分流時依 SLA 計算；CR 為空）"),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "實際解決時間（PM 結案時寫入）"),
                    FixerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯 CR 識別碼（CR 模組完成後使用）"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.CheckConstraint("CK_Tickets_FixerNotVerifier", "[FixerUserId] IS NULL OR [VerifierUserId] IS NULL OR [FixerUserId] <> [VerifierUserId]");
                    table.ForeignKey(
                        name: "FK_Tickets_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "客戶問題單：單號、分流、SLA、修復與驗證（修復人≠驗證人）");

            migrationBuilder.CreateTable(
                name: "TicketSequences",
                schema: "pms",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastNo = table.Column<int>(type: "int", nullable: false, comment: "最後使用的流水號"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSequences", x => x.ProjectId);
                },
                comment: "專案單號流水號（每專案一列），以 RowVersion 樂觀並行遞增");

            migrationBuilder.CreateTable(
                name: "TicketAttachments",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "FMS 檔案識別碼"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false, comment: "檔名（系統依 SOP 5.5：YYYYMMDD_[單號]_檔案說明）"),
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
                    table.PrimaryKey("PK_TicketAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAttachments_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "pms",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "問題單附件索引：實體檔案儲存於 FMS，僅記錄 FileId");

            migrationBuilder.CreateTable(
                name: "TicketEvents",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<byte>(type: "tinyint", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "操作人顯示名稱（帳號）"),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEvents_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "pms",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "問題單事件時間軸：建立、分流、指派、修復、驗證、結案");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_FileId",
                schema: "pms",
                table: "TicketAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_TicketId_IsDeleted",
                schema: "pms",
                table: "TicketAttachments",
                columns: new[] { "TicketId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketEvents_TicketId_At",
                schema: "pms",
                table: "TicketEvents",
                columns: new[] { "TicketId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FixerUserId",
                schema: "pms",
                table: "Tickets",
                column: "FixerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ProjectId_Status_DueAt",
                schema: "pms",
                table: "Tickets",
                columns: new[] { "ProjectId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ReporterUserId",
                schema: "pms",
                table: "Tickets",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketNo",
                schema: "pms",
                table: "Tickets",
                column: "TicketNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_VerifierUserId",
                schema: "pms",
                table: "Tickets",
                column: "VerifierUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Holidays",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "TicketAttachments",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "TicketEvents",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "TicketSequences",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "pms");
        }
    }
}
