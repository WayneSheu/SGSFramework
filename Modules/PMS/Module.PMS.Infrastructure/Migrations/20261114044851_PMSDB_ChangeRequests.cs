using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_ChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CrNo = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false, comment: "CR 單號（專案代號-CR-流水號，例 PRJ-CR-001）"),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "需求描述"),
                    SourceTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "來源問題單（由問題單分流為 CR 而建立時）"),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EvalDueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "影響評估期限：受理後 2 個工作天（SOP 4.4）"),
                    Decision = table.Column<byte>(type: "tinyint", nullable: true, comment: "決策方案"),
                    DecisionRationale = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "決策說明"),
                    NewReleaseDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "方案 A：新的上線日"),
                    SwappedOutItems = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "方案 B：移出本次迭代的原定任務"),
                    QuoteAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true, comment: "客戶 CR 報價金額"),
                    DecidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ContentHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true, comment: "決策時內容的 SHA-256（簽核者看到的內容必須與之相符，簽核後內容鎖定）"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "需求變更單 CR：客戶 CR（報價）與內部 CR；評估、決策與簽核");

            migrationBuilder.CreateTable(
                name: "ChangeRequestSequences",
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
                    table.PrimaryKey("PK_ChangeRequestSequences", x => x.ProjectId);
                },
                comment: "CR 單號流水號（每專案一列），以 RowVersion 樂觀並行遞增");

            migrationBuilder.CreateTable(
                name: "CrEvents",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<byte>(type: "tinyint", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrEvents_ChangeRequests_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalSchema: "pms",
                        principalTable: "ChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "CR 事件時間軸：建立、評估、決策、簽核、核准、取消");

            migrationBuilder.CreateTable(
                name: "CrImpactAssessments",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AffectedLayers = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "受影響層級（逗號分隔：Presentation、Api、Application、Domain、Infrastructure）"),
                    ApiImpact = table.Column<byte>(type: "tinyint", nullable: false),
                    CouplingImpact = table.Column<byte>(type: "tinyint", nullable: false),
                    SecurityImpact = table.Column<bool>(type: "bit", nullable: false, comment: "資安影響：是則須 Design Gate 重審"),
                    HoursBe = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    HoursFe = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    HoursVerify = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvaluatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrImpactAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrImpactAssessments_ChangeRequests_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalSchema: "pms",
                        principalTable: "ChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "CR 影響評估：受影響層級、API 契約、模組耦合、資安影響與 BE／FE／驗證工時");

            migrationBuilder.CreateTable(
                name: "CrSignoffs",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    SignerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "簽核人使用者識別碼（客戶代錄時為代錄的 PM）"),
                    SignerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, comment: "簽核當下的內容 SHA-256"),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsProxy = table.Column<bool>(type: "bit", nullable: false, comment: "是否由 PM 代錄客戶簽核（例外路徑，須附原因）"),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrSignoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrSignoffs_ChangeRequests_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalSchema: "pms",
                        principalTable: "ChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "CR 簽核紀錄：PM、Tech Lead 與客戶；含簽核當下內容雜湊，客戶簽核可由 PM 代錄");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_CrNo",
                schema: "pms",
                table: "ChangeRequests",
                column: "CrNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_ProjectId_Status_EvalDueAt",
                schema: "pms",
                table: "ChangeRequests",
                columns: new[] { "ProjectId", "Status", "EvalDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_RequesterUserId",
                schema: "pms",
                table: "ChangeRequests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_SourceTicketId",
                schema: "pms",
                table: "ChangeRequests",
                column: "SourceTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_CrEvents_ChangeRequestId_At",
                schema: "pms",
                table: "CrEvents",
                columns: new[] { "ChangeRequestId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_CrImpactAssessments_ChangeRequestId",
                schema: "pms",
                table: "CrImpactAssessments",
                column: "ChangeRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrSignoffs_ChangeRequestId_Role",
                schema: "pms",
                table: "CrSignoffs",
                columns: new[] { "ChangeRequestId", "Role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeRequestSequences",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "CrEvents",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "CrImpactAssessments",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "CrSignoffs",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ChangeRequests",
                schema: "pms");
        }
    }
}
