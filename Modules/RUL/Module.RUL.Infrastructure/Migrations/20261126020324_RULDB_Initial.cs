using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.RUL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RULDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rul");

            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                schema: "rul",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<long>(type: "bigint", nullable: true, comment: "既有規則 Id；Create 於核准後回填"),
                    ChangeType = table.Column<byte>(type: "tinyint", nullable: false),
                    ProposedJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true, comment: "提議的規則內容（JSON；Disable/Enable 為 null）"),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "申請理由"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                },
                comment: "規則變更申請（Maker-Checker）");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "rul",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "識別主鍵"),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "關聯 ID：代表整個業務流程的追蹤編號 (由前端或 API 傳入)"),
                    CausationId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "誘發 ID：代表觸發此事件的具體 Command 名稱或 ID"),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "事件的完整型別名稱 (包含 Namespace)，用於還原物件"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "事件序列化後的 JSON 內容"),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "事件發生的原始時間 (UTC)"),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "訊息被成功處理的時間。若為 NULL 且 IsDead 為 false，表示待處理；若為 1900-01-01，表示正被某個 Worker 鎖定處理中。"),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "預計下次重試的時間 (UTC)。用於實作「指數退避」，避免失敗後立即重試造成系統壓力。"),
                    RetryCount = table.Column<int>(type: "int", nullable: false, comment: "目前已重試的次數"),
                    IsDead = table.Column<bool>(type: "bit", nullable: false, comment: "當 RetryCount 超過閾值後標記為 true，背景服務將不再主動抓取。"),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "最後一次處理失敗的錯誤訊息內容，便於維運排錯")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                },
                comment: "Outbox 訊息實體，用於實作「發佈者確認」模式 (Publisher Confirmed)");

            migrationBuilder.CreateTable(
                name: "Rules",
                schema: "rul",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "規則名稱"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Domain = table.Column<byte>(type: "tinyint", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    TargetEntity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "目標實體（須為已登錄的白名單實體）"),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "欄位名稱（Column / Validation 規則）"),
                    RoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "適用角色（GlobalPolicy 代表所有人）"),
                    ConditionExpr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, comment: "條件運算式（System.Linq.Dynamic.Core，經白名單檢查）"),
                    ActionExpr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "計價折扣倍率運算式"),
                    OutputPayload = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "JSON 輸出（表單 visible/required/errorMessage；風控 decision/reason）"),
                    ReadAccess = table.Column<int>(type: "int", nullable: false),
                    WriteAccess = table.Column<int>(type: "int", nullable: false),
                    Risk = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "目前生效版本號"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                },
                comment: "動態規則主檔：僅存放已核准版本，異動須經 RuleChangeRequests 審批");

            migrationBuilder.CreateTable(
                name: "TestCases",
                schema: "rul",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "RuleUserContext JSON"),
                    TargetJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "目標實體 JSON"),
                    ExpectedMatch = table.Column<bool>(type: "bit", nullable: false, comment: "預期是否命中"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.Id);
                },
                comment: "規則測試案例");

            migrationBuilder.CreateTable(
                name: "Versions",
                schema: "rul",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "完整規則快照（JSON）"),
                    ChangeRequestId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Versions", x => x.Id);
                },
                comment: "核准後的規則版本快照");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_RuleId",
                schema: "rul",
                table: "ChangeRequests",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_Status_RequestedAtUtc",
                schema: "rul",
                table: "ChangeRequests",
                columns: new[] { "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Rules_Status_Domain_TargetEntity",
                schema: "rul",
                table: "Rules",
                columns: new[] { "Status", "Domain", "TargetEntity" });

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_RuleId",
                schema: "rul",
                table: "TestCases",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_RuleId_Version",
                schema: "rul",
                table: "Versions",
                columns: new[] { "RuleId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeRequests",
                schema: "rul");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "rul");

            migrationBuilder.DropTable(
                name: "Rules",
                schema: "rul");

            migrationBuilder.DropTable(
                name: "TestCases",
                schema: "rul");

            migrationBuilder.DropTable(
                name: "Versions",
                schema: "rul");
        }
    }
}
