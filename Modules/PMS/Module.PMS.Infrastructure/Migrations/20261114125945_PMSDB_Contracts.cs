using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.PMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PMSDB_Contracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contracts",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "合約編號 CT-YYYY-NNN，核發後不可變"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "合約／專案名稱"),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "客戶（甲方）名稱"),
                    CustomerContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "甲方窗口姓名"),
                    CustomerContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "甲方窗口 Email（正式通知用，個資）"),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Industry = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "合約總價金（新台幣，未稅）"),
                    QuoteDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "報價日"),
                    SignedDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "簽約日（用印完成日）"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "合約起日"),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "原定交付／驗收日（合約約定，變更與展延另計）"),
                    OpenEndedScope = table.Column<bool>(type: "bit", nullable: false, comment: "條款含「包含但不限於」等開放式範疇字眼"),
                    ReviewDeadlineWorkdays = table.Column<int>(type: "int", nullable: true, comment: "甲方文件審查期限（工作日）；null＝合約未約定期限"),
                    MaxReviewRounds = table.Column<int>(type: "int", nullable: true, comment: "彙整修改次數上限；null＝未約定"),
                    DeemedPassOnTimeout = table.Column<bool>(type: "bit", nullable: false, comment: "逾期未回覆視為通過"),
                    IpRule = table.Column<byte>(type: "tinyint", nullable: false),
                    ScanRule = table.Column<byte>(type: "tinyint", nullable: false),
                    DeemedAcceptanceWorkdays = table.Column<int>(type: "int", nullable: true, comment: "擬制驗收期限（交付後工作日）；null＝無擬制驗收條款"),
                    FinalPaymentHeldForMinorDefects = table.Column<bool>(type: "bit", nullable: false, comment: "允許因微幅瑕疵扣留全部尾款"),
                    LiabilityCap = table.Column<byte>(type: "tinyint", nullable: false),
                    LiabilityCapAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true, comment: "賠償上限金額（上限類型為 FixedAmount 時使用）"),
                    ExcludesIndirectLoss = table.Column<bool>(type: "bit", nullable: false, comment: "排除間接損害與所失利益"),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: false, comment: "免費保固月數"),
                    IncidentNotifyHours = table.Column<int>(type: "int", nullable: true, comment: "重大資安事件正式通報時限（小時）；null＝未約定"),
                    AuditNoticeDays = table.Column<int>(type: "int", nullable: true, comment: "實地稽核須事前通知天數；null＝未約定"),
                    AuditPerYear = table.Column<int>(type: "int", nullable: true, comment: "每年實地稽核次數上限；null＝未約定"),
                    AuditCostOnVendor = table.Column<bool>(type: "bit", nullable: false, comment: "稽核費用由乙方負擔"),
                    ServiceHours = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "標準服務時間"),
                    SupportChannel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "唯一正式報修管道（客服信箱或系統）"),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "備註"),
                    ApprovalCycle = table.Column<int>(type: "int", nullable: false, comment: "送審週期（每次送審加一；退回後再送審為新週期）"),
                    SnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "送審時鎖定的條款與審閱結果 SHA-256；核決與簽約前比對"),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SignedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                },
                comment: "專案合約主檔：條款參數、狀態、核決快照；僅合約成員可見");

            migrationBuilder.CreateTable(
                name: "ContractApprovals",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cycle = table.Column<int>(type: "int", nullable: false),
                    Step = table.Column<byte>(type: "tinyint", nullable: false),
                    Decision = table.Column<byte>(type: "tinyint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractApprovals_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "合約核決紀錄（PM 初審、法務審、高階主管核決）");

            migrationBuilder.CreateTable(
                name: "ContractAttachments",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAttachments_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "合約附件中繼資料；檔案內容存放於檔案管理（FMS）");

            migrationBuilder.CreateTable(
                name: "ContractDeliverableItems",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "階段（規劃、分析、設計、測試、訓練、結案…）"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "交付項目名稱"),
                    Format = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "交付形式"),
                    AcceptMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "確認／驗收方式"),
                    RequiresCustomerSignoff = table.Column<bool>(type: "bit", nullable: false, comment: "需甲方書面簽認（第二期：文件審查計時依據）"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractDeliverableItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractDeliverableItems_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "附件一交付項目清單：正面表列的交付物與確認方式");

            migrationBuilder.CreateTable(
                name: "ContractEvents",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractEvents_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "合約事件軌跡：狀態轉移、核決、簽約與異動");

            migrationBuilder.CreateTable(
                name: "ContractMembers",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractMembers_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "合約成員與角色（業務／PM／法務／核決人）；資料列授權依據");

            migrationBuilder.CreateTable(
                name: "ContractProjects",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractProjects_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "pms",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "合約綁定的 PMS 專案");

            migrationBuilder.CreateTable(
                name: "ContractReviewItems",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "項目代碼（SCOPE、DOC_REVIEW、IPR、VULN_SCAN、ACCEPTANCE、LIABILITY、SECURITY）"),
                    ClientClause = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true, comment: "甲方原條款（摘錄）"),
                    Stance = table.Column<byte>(type: "tinyint", nullable: false),
                    Result = table.Column<byte>(type: "tinyint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true, comment: "審閱說明或折衷方案"),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractReviewItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractReviewItems_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "審約檢核表：甲方原條款、乙方立場與審閱結果");

            migrationBuilder.CreateTable(
                name: "ContractSlaLevels",
                schema: "pms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false, comment: "等級 1~3"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Definition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResponseHours = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false, comment: "回應時間（標準服務時間內，小時）"),
                    Target = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "處理目標說明"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSlaLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractSlaLevels_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "pms",
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "附件二 SLA 分級：定義、回應時間與處理目標");

            migrationBuilder.CreateIndex(
                name: "IX_ContractApprovals_ContractId_Cycle_Step",
                schema: "pms",
                table: "ContractApprovals",
                columns: new[] { "ContractId", "Cycle", "Step" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractAttachments_ContractId_IsDeleted",
                schema: "pms",
                table: "ContractAttachments",
                columns: new[] { "ContractId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractDeliverableItems_ContractId_SortOrder",
                schema: "pms",
                table: "ContractDeliverableItems",
                columns: new[] { "ContractId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractEvents_ContractId_At",
                schema: "pms",
                table: "ContractEvents",
                columns: new[] { "ContractId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractMembers_ContractId_UserId_Role",
                schema: "pms",
                table: "ContractMembers",
                columns: new[] { "ContractId", "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractMembers_UserId",
                schema: "pms",
                table: "ContractMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractProjects_ContractId",
                schema: "pms",
                table: "ContractProjects",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractProjects_ProjectId",
                schema: "pms",
                table: "ContractProjects",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractReviewItems_ContractId_Code",
                schema: "pms",
                table: "ContractReviewItems",
                columns: new[] { "ContractId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractNo",
                schema: "pms",
                table: "Contracts",
                column: "ContractNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CustomerName",
                schema: "pms",
                table: "Contracts",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status_DeliveryDate",
                schema: "pms",
                table: "Contracts",
                columns: new[] { "Status", "DeliveryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractSlaLevels_ContractId_Level",
                schema: "pms",
                table: "ContractSlaLevels",
                columns: new[] { "ContractId", "Level" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractApprovals",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractAttachments",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractDeliverableItems",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractEvents",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractMembers",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractProjects",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractReviewItems",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "ContractSlaLevels",
                schema: "pms");

            migrationBuilder.DropTable(
                name: "Contracts",
                schema: "pms");
        }
    }
}
