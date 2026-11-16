using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGS.Modules.QTN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QTNDB_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "qtn");

            migrationBuilder.CreateTable(
                name: "ApprovalLogs",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<byte>(type: "tinyint", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: true),
                    Cycle = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "操作者名稱"),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "來源 IP 位址"),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "時間戳記（UTC）"),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "說明／意見"),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "當下內容雜湊")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalLogs", x => x.Id);
                },
                comment: "報價稽核軌跡：動作、操作者 ID、IP、時間戳記（只增不改）");

            migrationBuilder.CreateTable(
                name: "ApproverAssignments",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApproverAssignments", x => x.Id);
                },
                comment: "審批權責矩陣人選：角色 × 使用者");

            migrationBuilder.CreateTable(
                name: "ChangeAssessments",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, comment: "評估單號 {報價單號}-CR{NN}"),
                    BaseQuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "原報價版本（須為已成交版本）"),
                    CrQuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "衍生之 CR 報價版本"),
                    Requester = table.Column<byte>(type: "tinyint", nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "提出人"),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "收到請求日"),
                    AssessmentDueDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "評估期限（收到後 3 個工作日）"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "變更標題"),
                    OriginalSpec = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "原規格定義"),
                    ChangedSpec = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "變更後需求"),
                    NewDevDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "新增開發工時（人天）"),
                    ReworkDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "既有重工工時（人天）"),
                    RegressionDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "回歸測試工時（人天）"),
                    PmDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "專案管理工時（人天）"),
                    AmountUntaxed = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "追加金額（未稅；取自 CR 報價版本）"),
                    AmountWithTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "追加金額（含稅）"),
                    DelayWorkdays = table.Column<int>(type: "int", nullable: false, comment: "對 UAT／上線日之順延工作日數"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CustomerSignatory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "客戶授權代表人"),
                    CustomerSignedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeAssessments", x => x.Id);
                },
                comment: "專案變更影響評估單：差異、量化工時、追加金額、時程衝擊");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "客戶代碼（唯一）"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "客戶公司全銜"),
                    TaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, comment: "統一編號"),
                    Industry = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "產業別"),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true, comment: "地址"),
                    ContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "聯絡窗口姓名（個資）"),
                    ContactEmail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "聯絡窗口 Email（個資，落地加密）"),
                    ContactPhone = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true, comment: "聯絡窗口電話（個資，落地加密）"),
                    IsStrategic = table.Column<bool>(type: "bit", nullable: false, comment: "戰略標竿客戶（毛利率低於 20% 時可申請特批）"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, comment: "停用（停用後不可建立新商機）"),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "備註"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                },
                comment: "報價客戶主檔：統編、名稱、聯絡窗口（個資加密）");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "qtn",
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
                name: "RateRoles",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "角色代碼（唯一，例：SA、BE-SR、QA）"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "角色名稱"),
                    BaseCostRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "內部成本單價（新台幣／人天，機密）"),
                    BaseListRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "對外牌價（新台幣／人天）"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "排序"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, comment: "停用"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateRoles", x => x.Id);
                },
                comment: "角色費率：內部成本單價（機密）與對外牌價（人天）");

            migrationBuilder.CreateTable(
                name: "VarianceReviews",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "比對之已成交報價版本"),
                    ClosedDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "專案結案日"),
                    ReportDueDate = table.Column<DateOnly>(type: "date", nullable: false, comment: "報告期限（結案後 14 個工作日）"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RetrospectiveNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true, comment: "復盤會議結論"),
                    CalibrationDueDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "模組庫校準期限（復盤後 7 日）"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VarianceReviews", x => x.Id);
                },
                comment: "預估與實際工時對比報告：結案後 14 個工作日內產出");

            migrationBuilder.CreateTable(
                name: "WbsTemplates",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "模組代碼（唯一）"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "模組名稱"),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "說明"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, comment: "停用"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WbsTemplates", x => x.Id);
                },
                comment: "標準 WBS 工時庫：模組範本");

            migrationBuilder.CreateTable(
                name: "Opportunities",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "商機編號 OP-YYYY-NNN"),
                    CrmRef = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "CRM 商機編號（外部系統）"),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "專案名稱"),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    PricingModel = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true, comment: "業務邊界／需求摘要（初步需求規格清單）"),
                    ExpectedGoLive = table.Column<DateOnly>(type: "date", nullable: true, comment: "預期上線日"),
                    ConcurrentUsers = table.Column<int>(type: "int", nullable: true, comment: "預估併發量"),
                    SecurityLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "資安等級要求"),
                    NonFunctional = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "其他非功能性需求"),
                    IpBuyout = table.Column<bool>(type: "bit", nullable: false, comment: "涉及底層核心原始碼（IP）買斷轉讓（須法務長會簽並加計 ≥30% 授權費）"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Opportunities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "qtn",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "報價商機：關聯 CRM 商機編號、客戶、計價模式與非功能性需求");

            migrationBuilder.CreateTable(
                name: "RateRoleHistories",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "成本單價"),
                    ListRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "牌價"),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "生效時間"),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "異動原因"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateRoleHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateRoleHistories_RateRoles_RateRoleId",
                        column: x => x.RateRoleId,
                        principalSchema: "qtn",
                        principalTable: "RateRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "角色費率異動歷程");

            migrationBuilder.CreateTable(
                name: "VarianceItems",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WbsItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WbsCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "WBS 編號"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "項目名稱"),
                    QuotedDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "報價預估人天"),
                    ActualDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true, comment: "實際消耗人天"),
                    VarianceRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true, comment: "偏差率（實際－預估）／預估"),
                    ExceedsThreshold = table.Column<bool>(type: "bit", nullable: false, comment: "偏差超過 ±15%"),
                    RootCause = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "偏差原因"),
                    Calibrated = table.Column<bool>(type: "bit", nullable: false, comment: "已校準標準工時庫"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VarianceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VarianceItems_VarianceReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalSchema: "qtn",
                        principalTable: "VarianceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "WBS 項目預估 vs 實際工時與偏差率（±15% 須復盤校準）");

            migrationBuilder.CreateTable(
                name: "WbsTemplateItems",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "工作項名稱"),
                    Deliverable = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "核心交付物"),
                    Phase = table.Column<byte>(type: "tinyint", nullable: false),
                    RateRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "預設投入角色"),
                    Optimistic = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "最樂觀工時 O（人天）"),
                    MostLikely = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "最可能工時 M（人天）"),
                    Pessimistic = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "最悲觀工時 P（人天）"),
                    BaselineVersion = table.Column<int>(type: "int", nullable: false, comment: "基準版本（每次復盤校準 +1）"),
                    LastCalibratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "最近一次校準時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WbsTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WbsTemplateItems_WbsTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "qtn",
                        principalTable: "WbsTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "標準 WBS 工作項：階段、角色、預設三點工時（人天）");

            migrationBuilder.CreateTable(
                name: "OpportunityMembers",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpportunityMembers_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "qtn",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "商機成員：業務／Tech Lead／PM／財務（資料列授權依據）");

            migrationBuilder.CreateTable(
                name: "QuoteVersions",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "報價單號 QT-{FP|AG|HB|RT}-YYYYMMDD-NN（同商機各版本共用）"),
                    Major = table.Column<int>(type: "int", nullable: false, comment: "主版本"),
                    Minor = table.Column<int>(type: "int", nullable: false, comment: "次版本"),
                    IsChangeRequest = table.Column<bool>(type: "bit", nullable: false, comment: "是否為 CR 變更追加版本"),
                    VersionLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, comment: "版本標籤 v1.0 / v1.1 / v1.2-CR"),
                    ParentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "衍生來源版本"),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PricingModel = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "報價標題"),
                    TechReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "Tech Lead 完成工時評估與技術可行性複核"),
                    TechReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TechReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "技術複核意見"),
                    TotalQuotedDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "報價總人天"),
                    LaborCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "直接人力成本"),
                    Overhead = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "管銷分攤（直接人力成本 × 18%）"),
                    DirectExpense = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "專案直接費用"),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "專案總成本"),
                    LaborListTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "人力牌價小計"),
                    IpLicenseFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "IP 買斷授權費"),
                    ListTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "牌價總計（未稅）"),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "專案合作優惠（折扣金額）"),
                    DiscountReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "折扣說明"),
                    FinalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "成交總價（未稅）"),
                    TaxRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false, comment: "營業稅率"),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "營業稅額"),
                    FinalPriceWithTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "成交總價（含稅）"),
                    MarginRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false, comment: "預估毛利率（0~1）"),
                    MarginLight = table.Column<byte>(type: "tinyint", nullable: false),
                    BonusFactor = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false, comment: "業務獎金提成係數（綠 1.0／黃 0.8／紅 0.5）"),
                    SpecialApproval = table.Column<bool>(type: "bit", nullable: false, comment: "戰略標竿客戶特批（毛利率低於 20% 時才可送出）"),
                    SpecialApprovalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "特批理由"),
                    SprintCount = table.Column<int>(type: "int", nullable: true, comment: "T&M：基礎合約 Sprint 數（每 Sprint 10 個工作日）"),
                    RetainerMonths = table.Column<int>(type: "int", nullable: true, comment: "Retainer：合約月數"),
                    RetainerStart = table.Column<DateOnly>(type: "date", nullable: true, comment: "Retainer：合約起日"),
                    RetainerMonthlyDaysCap = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true, comment: "Retainer：每月標準維運人天上限"),
                    RetainerMonthlyFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true, comment: "Retainer：每月固定月費（未稅）"),
                    OverageDailyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true, comment: "Retainer：超額人天費率"),
                    OffHoursMultiplier = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true, comment: "Retainer：假日／夜間非緊急變更工時加乘"),
                    DurationWeeks = table.Column<int>(type: "int", nullable: true, comment: "預計總工期（週）"),
                    ValidDays = table.Column<int>(type: "int", nullable: false, comment: "報價有效日曆天數（預設 30）"),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: true, comment: "出具日（對外發出日）"),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true, comment: "報價有效截止日"),
                    ApprovalCycle = table.Column<int>(type: "int", nullable: false, comment: "送審輪次"),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "送審時凍結內容 SHA-256（核准時比對）"),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, comment: "鎖定（送審後不可修改，須衍生新版本）"),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OutcomeNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "成交／未得標原因"),
                    PmsContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "已建立之 PMS 合約識別碼（跨 schema，無外鍵）"),
                    PmsContractNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true, comment: "已建立之 PMS 合約編號"),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "內部備註"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteVersions_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalSchema: "qtn",
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "報價版本：狀態機、總成本、最終報價、毛利率、燈號與鎖定標記");

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cycle = table.Column<int>(type: "int", nullable: false),
                    Seq = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<byte>(type: "tinyint", nullable: false),
                    State = table.Column<byte>(type: "tinyint", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "決定者姓名"),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "審批意見"),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "決定當下比對之內容雜湊"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalSchema: "qtn",
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "審批關卡：輪次、順序、角色與決定（含送審內容雜湊）");

            migrationBuilder.CreateTable(
                name: "QuoteAttachments",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, comment: "系統檔名"),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, comment: "原始檔名"),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteAttachments_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalSchema: "qtn",
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "報價附件（檔案存放於 FMS，僅存 FileId 索引）");

            migrationBuilder.CreateTable(
                name: "QuoteClauses",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, comment: "條款代碼"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "條款標題"),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "條款內容"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteClauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteClauses_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalSchema: "qtn",
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "對外報價單商業防禦條款（附錄 A 與範本專屬條款）");

            migrationBuilder.CreateTable(
                name: "QuoteDirectExpenses",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, comment: "說明"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "金額（未稅）"),
                    IsCapped = table.Column<bool>(type: "bit", nullable: false, comment: "實報實銷上限"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteDirectExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteDirectExpenses_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalSchema: "qtn",
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "專案直接費用：雲端、授權、差旅");

            migrationBuilder.CreateTable(
                name: "QuotePaymentMilestones",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Seq = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "期別名稱"),
                    Percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, comment: "比例（%）"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "金額（未稅）"),
                    AmountWithTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "金額（含稅）"),
                    Trigger = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "請款條件"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotePaymentMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotePaymentMilestones_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalSchema: "qtn",
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "付款里程碑（固定總價預設 30/20/30/20）");

            migrationBuilder.CreateTable(
                name: "QuoteSlaLevels",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, comment: "等級（P1／P2／P3）"),
                    Definition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "定義說明"),
                    ResponseTime = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "首次回應時間"),
                    ResolveTarget = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "目標解決／緩解時間"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteSlaLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteSlaLevels_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalSchema: "qtn",
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "維運報價 SLA 分級：首次回應與目標解決時間");

            migrationBuilder.CreateTable(
                name: "QuoteWbsItems",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "上層 WBS（null＝第一層）"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    WbsCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, comment: "WBS 編號（1.0、1.1、2.3.1；由系統依樹狀位置產生）"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "模組／階段名稱"),
                    Deliverable = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true, comment: "核心交付物說明"),
                    Phase = table.Column<byte>(type: "tinyint", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false, comment: "混合計價階段（1＝前置健檢固定總價；2＝開發期 T&M）"),
                    KTech = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false, comment: "技術新穎度係數 K技術（1.0／1.2／1.5）"),
                    KReq = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false, comment: "需求明確度係數 K需求（1.0／1.3／1.6）"),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "來源標準工作項（供復盤校準）"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteWbsItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteWbsItems_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalSchema: "qtn",
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "報價 WBS 項目：樹狀結構，技術新穎度與需求明確度係數");

            migrationBuilder.CreateTable(
                name: "WbsRoleAllocations",
                schema: "qtn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WbsItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "角色名稱快照"),
                    Optimistic = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "最樂觀工時 O"),
                    MostLikely = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "最可能工時 M"),
                    Pessimistic = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "最悲觀工時 P"),
                    ExpectedDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "期望工時 E＝(O+4M+P)/6"),
                    QuotedDays = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, comment: "最終報價人天＝E×K技術×K需求"),
                    SnapshotCostRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "成本單價快照"),
                    SnapshotListRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, comment: "牌價快照"),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WbsRoleAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WbsRoleAllocations_QuoteWbsItems_WbsItemId",
                        column: x => x.WbsItemId,
                        principalSchema: "qtn",
                        principalTable: "QuoteWbsItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "WBS 角色工時：PERT 三點工時、報價人天、費率快照");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalLogs_QuoteVersionId_At",
                schema: "qtn",
                table: "ApprovalLogs",
                columns: new[] { "QuoteVersionId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_QuoteVersionId_Cycle_Seq",
                schema: "qtn",
                table: "ApprovalSteps",
                columns: new[] { "QuoteVersionId", "Cycle", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_Role_State",
                schema: "qtn",
                table: "ApprovalSteps",
                columns: new[] { "Role", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_ApproverAssignments_Role_UserId",
                schema: "qtn",
                table: "ApproverAssignments",
                columns: new[] { "Role", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeAssessments_AssessmentNo",
                schema: "qtn",
                table: "ChangeAssessments",
                column: "AssessmentNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeAssessments_OpportunityId",
                schema: "qtn",
                table: "ChangeAssessments",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeAssessments_Status_AssessmentDueDate",
                schema: "qtn",
                table: "ChangeAssessments",
                columns: new[] { "Status", "AssessmentDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Code",
                schema: "qtn",
                table: "Customers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_CustomerId",
                schema: "qtn",
                table: "Opportunities",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_OpportunityNo",
                schema: "qtn",
                table: "Opportunities",
                column: "OpportunityNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_Status",
                schema: "qtn",
                table: "Opportunities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityMembers_OpportunityId_UserId_Role",
                schema: "qtn",
                table: "OpportunityMembers",
                columns: new[] { "OpportunityId", "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityMembers_UserId",
                schema: "qtn",
                table: "OpportunityMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteAttachments_FileId",
                schema: "qtn",
                table: "QuoteAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteAttachments_QuoteVersionId",
                schema: "qtn",
                table: "QuoteAttachments",
                column: "QuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteClauses_QuoteVersionId",
                schema: "qtn",
                table: "QuoteClauses",
                column: "QuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDirectExpenses_QuoteVersionId",
                schema: "qtn",
                table: "QuoteDirectExpenses",
                column: "QuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotePaymentMilestones_QuoteVersionId_Seq",
                schema: "qtn",
                table: "QuotePaymentMilestones",
                columns: new[] { "QuoteVersionId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteSlaLevels_QuoteVersionId",
                schema: "qtn",
                table: "QuoteSlaLevels",
                column: "QuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteVersions_OpportunityId",
                schema: "qtn",
                table: "QuoteVersions",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteVersions_QuoteNo_Major_Minor",
                schema: "qtn",
                table: "QuoteVersions",
                columns: new[] { "QuoteNo", "Major", "Minor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteVersions_Status",
                schema: "qtn",
                table: "QuoteVersions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteVersions_ValidUntil",
                schema: "qtn",
                table: "QuoteVersions",
                column: "ValidUntil");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteWbsItems_QuoteVersionId_ParentId_SortOrder",
                schema: "qtn",
                table: "QuoteWbsItems",
                columns: new[] { "QuoteVersionId", "ParentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RateRoleHistories_RateRoleId_EffectiveAt",
                schema: "qtn",
                table: "RateRoleHistories",
                columns: new[] { "RateRoleId", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RateRoles_Code",
                schema: "qtn",
                table: "RateRoles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VarianceItems_ReviewId",
                schema: "qtn",
                table: "VarianceItems",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_VarianceReviews_QuoteVersionId",
                schema: "qtn",
                table: "VarianceReviews",
                column: "QuoteVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WbsRoleAllocations_RateRoleId",
                schema: "qtn",
                table: "WbsRoleAllocations",
                column: "RateRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsRoleAllocations_WbsItemId",
                schema: "qtn",
                table: "WbsRoleAllocations",
                column: "WbsItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsTemplateItems_TemplateId",
                schema: "qtn",
                table: "WbsTemplateItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WbsTemplates_Code",
                schema: "qtn",
                table: "WbsTemplates",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalLogs",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "ApprovalSteps",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "ApproverAssignments",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "ChangeAssessments",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "OpportunityMembers",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "QuoteAttachments",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "QuoteClauses",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "QuoteDirectExpenses",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "QuotePaymentMilestones",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "QuoteSlaLevels",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "RateRoleHistories",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "VarianceItems",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "WbsRoleAllocations",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "WbsTemplateItems",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "RateRoles",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "VarianceReviews",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "QuoteWbsItems",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "WbsTemplates",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "QuoteVersions",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "Opportunities",
                schema: "qtn");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "qtn");
        }
    }
}
