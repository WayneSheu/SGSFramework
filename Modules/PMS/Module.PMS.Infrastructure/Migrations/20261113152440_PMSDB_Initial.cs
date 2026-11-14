using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pms");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "pms",
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

            migrationBuilder.CreateTable(
                name: "ProjectCodeReservations",
                schema: "pms",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, comment: "專案代號（主鍵）"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCodeReservations", x => x.Code);
                },
                comment: "專案代號保留表：核發後永不重用");

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, comment: "專案代號（2~10 碼大寫英數、首碼為字母），核發後不可變"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "專案名稱"),
                    Phase = table.Column<byte>(type: "tinyint", nullable: false, comment: "目前階段（六階段）"),
                    Stage = table.Column<byte>(type: "tinyint", nullable: false, comment: "期別：UAT 驗收期／SLA 維運期"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, comment: "專案狀態"),
                    PmUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "PM 使用者識別碼"),
                    TechLeadUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "Tech Lead 使用者識別碼"),
                    FreezeLineDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "需求凍結線：此日期後任何規格變更須走 CR"),
                    ContractSlaNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "合約 SLA 覆寫備註（以合約為準時註記）"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false, comment: "並行控制"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.CheckConstraint("CK_Projects_Code_Format", "LEN([Code]) BETWEEN 2 AND 10 AND [Code] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z0-9]%' AND [Code] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z]%'");
                },
                comment: "專案主檔：專案代號由 PMS 核發，建立後不可修改");

            migrationBuilder.CreateTable(
                name: "PmsModules",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, comment: "模組代號 Mxx"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PmsModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PmsModules_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "專案模組目錄：Mxx 代號於文件資料夾與命名共用");

            migrationBuilder.CreateTable(
                name: "ProjectGates",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Gate = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EvidenceNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectGates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectGates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "SSDLC 四道卡控節點放行紀錄");

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaciRole = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "專案成員與 RACI 角色；亦為資料列授權依據");

            migrationBuilder.CreateTable(
                name: "Deliverables",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Stage = table.Column<byte>(type: "tinyint", nullable: false),
                    DocType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "文件類型（SRS、OpenAPI、ERD、SAST…）"),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "FMS 檔案識別碼"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false, comment: "檔名（已通過命名鐵律檢查）"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliverables_PmsModules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "pms",
                        principalTable: "PmsModules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deliverables_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "交付物索引：附件實體儲存於 FMS，僅記錄 FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_FileId",
                schema: "pms",
                table: "Deliverables",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_ModuleId",
                schema: "pms",
                table: "Deliverables",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_ProjectId_Stage_DocType",
                schema: "pms",
                table: "Deliverables",
                columns: new[] { "ProjectId", "Stage", "DocType" });

            migrationBuilder.CreateIndex(
                name: "IX_pms_OutboxMessages_CorrelationId",
                schema: "pms",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_pms_OutboxMessages_FetchPending",
                schema: "pms",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "IsDead", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PmsModules_ProjectId_Code",
                schema: "pms",
                table: "PmsModules",
                columns: new[] { "ProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGates_ProjectId_Gate",
                schema: "pms",
                table: "ProjectGates",
                columns: new[] { "ProjectId", "Gate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId_UserId_RaciRole",
                schema: "pms",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "UserId", "RaciRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_UserId",
                schema: "pms",
                table: "ProjectMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                schema: "pms",
                table: "Projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_Stage",
                schema: "pms",
                table: "Projects",
                columns: new[] { "Status", "Stage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliverables",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ProjectCodeReservations",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ProjectGates",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ProjectMembers",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "PmsModules",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "pms");
        }
    }
}
