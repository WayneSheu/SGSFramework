using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.Base;

namespace SGS.Modules.QTN.Infrastructure.Entities;

// =====================================================================
// 主檔：客戶、角色費率、標準 WBS 工時庫、審批人指派
// =====================================================================

/// <summary>客戶主檔（輕量）：報價對象；聯絡人個資落地加密。</summary>
[DisplayName("報價客戶")]
[Comment("報價客戶主檔：統編、名稱、聯絡窗口（個資加密）")]
public class Customer : IAuditable, IConcurrencyAware
{
    public Guid Id { get; set; }

    [Comment("客戶代碼（唯一）")]
    public string Code { get; set; } = string.Empty;

    [Comment("客戶公司全銜")]
    public string Name { get; set; } = string.Empty;

    [Comment("統一編號")]
    public string? TaxId { get; set; }

    [Comment("產業別")]
    public string? Industry { get; set; }

    [Comment("地址")]
    public string? Address { get; set; }

    [Masked]
    [Comment("聯絡窗口姓名（個資）")]
    public string? ContactName { get; set; }

    [Encrypted]
    [Comment("聯絡窗口 Email（個資，落地加密）")]
    public string? ContactEmail { get; set; }

    [Encrypted]
    [Comment("聯絡窗口電話（個資，落地加密）")]
    public string? ContactPhone { get; set; }

    [Comment("戰略標竿客戶（毛利率低於 20% 時可申請特批）")]
    public bool IsStrategic { get; set; }

    [Comment("停用（停用後不可建立新商機）")]
    public bool IsActive { get; set; } = true;

    [Comment("備註")]
    public string? Notes { get; set; }

    public byte[] RowVersion { get; set; } = [];
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>
/// 角色費率（SOP 6.1 roles）：內部成本單價（企業最高機密）與對外牌價。報價建立當下快照至 <see cref="WbsRoleAllocation"/>，
/// 後續調整不回溯影響既有報價。
/// </summary>
[DisplayName("角色費率")]
[Comment("角色費率：內部成本單價（機密）與對外牌價（人天）")]
public class RateRole : IAuditable, IConcurrencyAware
{
    public Guid Id { get; set; }

    [Comment("角色代碼（唯一，例：SA、BE-SR、QA）")]
    public string Code { get; set; } = string.Empty;

    [Comment("角色名稱")]
    public string Name { get; set; } = string.Empty;

    [SensitiveData]
    [Comment("內部成本單價（新台幣／人天，機密）")]
    public decimal BaseCostRate { get; set; }

    [Comment("對外牌價（新台幣／人天）")]
    public decimal BaseListRate { get; set; }

    [Comment("排序")]
    public int SortOrder { get; set; }

    [Comment("停用")]
    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = [];
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public List<RateRoleHistory> History { get; set; } = [];
}

/// <summary>角色費率異動歷程（調薪／調價留痕）。</summary>
[DisplayName("角色費率歷程")]
[Comment("角色費率異動歷程")]
public class RateRoleHistory : IAuditable
{
    public Guid Id { get; set; }
    public Guid RateRoleId { get; set; }

    [SensitiveData]
    [Comment("成本單價")]
    public decimal CostRate { get; set; }

    [Comment("牌價")]
    public decimal ListRate { get; set; }

    [Comment("生效時間")]
    public DateTimeOffset EffectiveAt { get; set; }

    [Comment("異動原因")]
    public string? Reason { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public RateRole? RateRole { get; set; }
}

/// <summary>標準 WBS 工時庫（模組範本）：報價拆解的起點，結案復盤偏差超過 ±15% 時校準。</summary>
[DisplayName("標準 WBS 模組")]
[Comment("標準 WBS 工時庫：模組範本")]
public class WbsTemplate : IAuditable, IConcurrencyAware
{
    public Guid Id { get; set; }

    [Comment("模組代碼（唯一）")]
    public string Code { get; set; } = string.Empty;

    [Comment("模組名稱")]
    public string Name { get; set; } = string.Empty;

    [Comment("說明")]
    public string? Description { get; set; }

    [Comment("停用")]
    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = [];
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public List<WbsTemplateItem> Items { get; set; } = [];
}

/// <summary>標準 WBS 工作項（含預設三點工時與角色），為工時校準對象。</summary>
[DisplayName("標準 WBS 工作項")]
[Comment("標準 WBS 工作項：階段、角色、預設三點工時（人天）")]
public class WbsTemplateItem : IAuditable
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int SortOrder { get; set; }

    [Comment("工作項名稱")]
    public string Name { get; set; } = string.Empty;

    [Comment("核心交付物")]
    public string? Deliverable { get; set; }

    public WbsPhase Phase { get; set; }

    [Comment("預設投入角色")]
    public Guid RateRoleId { get; set; }

    [Comment("最樂觀工時 O（人天）")]
    public decimal Optimistic { get; set; }

    [Comment("最可能工時 M（人天）")]
    public decimal MostLikely { get; set; }

    [Comment("最悲觀工時 P（人天）")]
    public decimal Pessimistic { get; set; }

    [Comment("基準版本（每次復盤校準 +1）")]
    public int BaselineVersion { get; set; } = 1;

    [Comment("最近一次校準時間")]
    public DateTimeOffset? LastCalibratedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public WbsTemplate? Template { get; set; }
}

/// <summary>審批人指派（SOP 5.2 審批權責矩陣中業務經理、PMO 總監、財務長、總經理、法務長之人選）。Tech Lead 關卡由商機的 Tech Lead 負責。</summary>
[DisplayName("報價審批人")]
[Comment("審批權責矩陣人選：角色 × 使用者")]
public class ApproverAssignment : IAuditable
{
    public Guid Id { get; set; }
    public ApprovalRole Role { get; set; }
    public Guid UserId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

// =====================================================================
// 商機（SOP 6.1 projects）
// =====================================================================

/// <summary>商機（報價專案）：客戶、計價模式、業務邊界與非功能性需求；底下有報價版本鏈。</summary>
[DisplayName("報價商機")]
[Comment("報價商機：關聯 CRM 商機編號、客戶、計價模式與非功能性需求")]
public class Opportunity : IAuditable, IConcurrencyAware
{
    public Guid Id { get; set; }

    [Comment("商機編號 OP-YYYY-NNN")]
    public string OpportunityNo { get; set; } = string.Empty;

    [Comment("CRM 商機編號（外部系統）")]
    public string? CrmRef { get; set; }

    public Guid CustomerId { get; set; }

    [Comment("專案名稱")]
    public string Title { get; set; } = string.Empty;

    public ProjectCategory Category { get; set; }
    public PricingModel PricingModel { get; set; }
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;

    [Comment("業務邊界／需求摘要（初步需求規格清單）")]
    public string? Scope { get; set; }

    [Comment("預期上線日")]
    public DateOnly? ExpectedGoLive { get; set; }

    [Comment("預估併發量")]
    public int? ConcurrentUsers { get; set; }

    [Comment("資安等級要求")]
    public string? SecurityLevel { get; set; }

    [Comment("其他非功能性需求")]
    public string? NonFunctional { get; set; }

    [Comment("涉及底層核心原始碼（IP）買斷轉讓（須法務長會簽並加計 ≥30% 授權費）")]
    public bool IpBuyout { get; set; }

    public byte[] RowVersion { get; set; } = [];
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Customer? Customer { get; set; }
    public List<OpportunityMember> Members { get; set; } = [];
    public List<QuoteVersion> Quotes { get; set; } = [];
}

[DisplayName("商機成員")]
[Comment("商機成員：業務／Tech Lead／PM／財務（資料列授權依據）")]
public class OpportunityMember : IAuditable
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid UserId { get; set; }
    public OpportunityRole Role { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Opportunity? Opportunity { get; set; }
}

// =====================================================================
// 報價版本（SOP 6.1 quote_versions）
// =====================================================================

/// <summary>
/// 報價版本：同一商機共用報價單號，以版本號（v1.0、v1.1、v1.2-CR）區分。進入審批後鎖定（is_locked），任何調整須衍生新版本；
/// 金額欄位為後端依規則重算，前端不可直接寫入。
/// </summary>
[DisplayName("報價版本")]
[Comment("報價版本：狀態機、總成本、最終報價、毛利率、燈號與鎖定標記")]
public class QuoteVersion : IAuditable, IConcurrencyAware
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }

    [Comment("報價單號 QT-{FP|AG|HB|RT}-YYYYMMDD-NN（同商機各版本共用）")]
    public string QuoteNo { get; set; } = string.Empty;

    [Comment("主版本")]
    public int Major { get; set; } = 1;

    [Comment("次版本")]
    public int Minor { get; set; }

    [Comment("是否為 CR 變更追加版本")]
    public bool IsChangeRequest { get; set; }

    [Comment("版本標籤 v1.0 / v1.1 / v1.2-CR")]
    public string VersionLabel { get; set; } = "v1.0";

    [Comment("衍生來源版本")]
    public Guid? ParentVersionId { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public PricingModel PricingModel { get; set; }

    [Comment("報價標題")]
    public string Title { get; set; } = string.Empty;

    // ---- 技術評估前置 ----
    [Comment("Tech Lead 完成工時評估與技術可行性複核")]
    public Guid? TechReviewedBy { get; set; }

    public DateTimeOffset? TechReviewedAt { get; set; }

    [Comment("技術複核意見")]
    public string? TechReviewNote { get; set; }

    // ---- 金額（未稅，新台幣；由規則重算） ----
    [Comment("報價總人天")]
    public decimal TotalQuotedDays { get; set; }

    [SensitiveData]
    [Comment("直接人力成本")]
    public decimal LaborCost { get; set; }

    [SensitiveData]
    [Comment("管銷分攤（直接人力成本 × 18%）")]
    public decimal Overhead { get; set; }

    [Comment("專案直接費用")]
    public decimal DirectExpense { get; set; }

    [SensitiveData]
    [Comment("專案總成本")]
    public decimal TotalCost { get; set; }

    [Comment("人力牌價小計")]
    public decimal LaborListTotal { get; set; }

    [Comment("IP 買斷授權費")]
    public decimal IpLicenseFee { get; set; }

    [Comment("牌價總計（未稅）")]
    public decimal ListTotal { get; set; }

    [Comment("專案合作優惠（折扣金額）")]
    public decimal DiscountAmount { get; set; }

    [Comment("折扣說明")]
    public string? DiscountReason { get; set; }

    [Comment("成交總價（未稅）")]
    public decimal FinalPrice { get; set; }

    [Comment("營業稅率")]
    public decimal TaxRate { get; set; } = 0.05m;

    [Comment("營業稅額")]
    public decimal TaxAmount { get; set; }

    [Comment("成交總價（含稅）")]
    public decimal FinalPriceWithTax { get; set; }

    [SensitiveData]
    [Comment("預估毛利率（0~1）")]
    public decimal MarginRate { get; set; }

    public MarginLight MarginLight { get; set; }

    [Comment("業務獎金提成係數（綠 1.0／黃 0.8／紅 0.5）")]
    public decimal BonusFactor { get; set; } = 1m;

    [Comment("戰略標竿客戶特批（毛利率低於 20% 時才可送出）")]
    public bool SpecialApproval { get; set; }

    [Comment("特批理由")]
    public string? SpecialApprovalReason { get; set; }

    // ---- 計價模式專屬參數 ----
    [Comment("T&M：基礎合約 Sprint 數（每 Sprint 10 個工作日）")]
    public int? SprintCount { get; set; }

    [Comment("Retainer：合約月數")]
    public int? RetainerMonths { get; set; }

    [Comment("Retainer：合約起日")]
    public DateOnly? RetainerStart { get; set; }

    [Comment("Retainer：每月標準維運人天上限")]
    public decimal? RetainerMonthlyDaysCap { get; set; }

    [Comment("Retainer：每月固定月費（未稅）")]
    public decimal? RetainerMonthlyFee { get; set; }

    [Comment("Retainer：超額人天費率")]
    public decimal? OverageDailyRate { get; set; }

    [Comment("Retainer：假日／夜間非緊急變更工時加乘")]
    public decimal? OffHoursMultiplier { get; set; }

    [Comment("預計總工期（週）")]
    public int? DurationWeeks { get; set; }

    // ---- 有效期與審批 ----
    [Comment("報價有效日曆天數（預設 30）")]
    public int ValidDays { get; set; } = 30;

    [Comment("出具日（對外發出日）")]
    public DateOnly? IssuedDate { get; set; }

    [Comment("報價有效截止日")]
    public DateOnly? ValidUntil { get; set; }

    [Comment("送審輪次")]
    public int ApprovalCycle { get; set; }

    [Comment("送審時凍結內容 SHA-256（核准時比對）")]
    public string? ContentHash { get; set; }

    [Comment("鎖定（送審後不可修改，須衍生新版本）")]
    public bool IsLocked { get; set; }

    public Guid? SubmittedBy { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    [Comment("成交／未得標原因")]
    public string? OutcomeNote { get; set; }

    // ---- 交付基準對齊 ----
    [Comment("已建立之 PMS 合約識別碼（跨 schema，無外鍵）")]
    public Guid? PmsContractId { get; set; }

    [Comment("已建立之 PMS 合約編號")]
    public string? PmsContractNo { get; set; }

    [Comment("內部備註")]
    public string? Notes { get; set; }

    public byte[] RowVersion { get; set; } = [];
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Opportunity? Opportunity { get; set; }
    public List<QuoteWbsItem> WbsItems { get; set; } = [];
    public List<QuoteDirectExpense> Expenses { get; set; } = [];
    public List<QuotePaymentMilestone> Milestones { get; set; } = [];
    public List<QuoteClause> Clauses { get; set; } = [];
    public List<QuoteSlaLevel> SlaLevels { get; set; } = [];
    public List<ApprovalStep> ApprovalSteps { get; set; } = [];
    public List<QuoteAttachment> Attachments { get; set; } = [];
}

/// <summary>報價 WBS 工作項（SOP 6.1 quote_wbs_items）：parent_wbs_id 支援無限層級；係數以葉節點為準。</summary>
[DisplayName("報價 WBS 項目")]
[Comment("報價 WBS 項目：樹狀結構，技術新穎度與需求明確度係數")]
public class QuoteWbsItem : IAuditable
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }

    [Comment("上層 WBS（null＝第一層）")]
    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }

    [Comment("WBS 編號（1.0、1.1、2.3.1；由系統依樹狀位置產生）")]
    public string WbsCode { get; set; } = string.Empty;

    [Comment("模組／階段名稱")]
    public string Name { get; set; } = string.Empty;

    [Comment("核心交付物說明")]
    public string? Deliverable { get; set; }

    public WbsPhase Phase { get; set; }

    [Comment("混合計價階段（1＝前置健檢固定總價；2＝開發期 T&M）")]
    public int Stage { get; set; } = 1;

    [Comment("技術新穎度係數 K技術（1.0／1.2／1.5）")]
    public decimal KTech { get; set; } = 1.0m;

    [Comment("需求明確度係數 K需求（1.0／1.3／1.6）")]
    public decimal KReq { get; set; } = 1.0m;

    [Comment("來源標準工作項（供復盤校準）")]
    public Guid? TemplateItemId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public QuoteVersion? QuoteVersion { get; set; }
    public List<WbsRoleAllocation> Allocations { get; set; } = [];
}

/// <summary>WBS 角色工時配置（SOP 6.1 wbs_role_allocations）：PERT 三點工時、最終報價人天與費率快照。</summary>
[DisplayName("WBS 角色工時")]
[Comment("WBS 角色工時：PERT 三點工時、報價人天、費率快照")]
public class WbsRoleAllocation : IAuditable
{
    public Guid Id { get; set; }
    public Guid WbsItemId { get; set; }
    public Guid RateRoleId { get; set; }

    [Comment("角色名稱快照")]
    public string RoleName { get; set; } = string.Empty;

    [Comment("最樂觀工時 O")]
    public decimal Optimistic { get; set; }

    [Comment("最可能工時 M")]
    public decimal MostLikely { get; set; }

    [Comment("最悲觀工時 P")]
    public decimal Pessimistic { get; set; }

    [Comment("期望工時 E＝(O+4M+P)/6")]
    public decimal ExpectedDays { get; set; }

    [Comment("最終報價人天＝E×K技術×K需求")]
    public decimal QuotedDays { get; set; }

    [SensitiveData]
    [Comment("成本單價快照")]
    public decimal SnapshotCostRate { get; set; }

    [Comment("牌價快照")]
    public decimal SnapshotListRate { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public QuoteWbsItem? WbsItem { get; set; }
}

[DisplayName("報價直接費用")]
[Comment("專案直接費用：雲端、授權、差旅")]
public class QuoteDirectExpense : IAuditable
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }
    public ExpenseCategory Category { get; set; }

    [Comment("說明")]
    public string Description { get; set; } = string.Empty;

    [Comment("金額（未稅）")]
    public decimal Amount { get; set; }

    [Comment("實報實銷上限")]
    public bool IsCapped { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

[DisplayName("報價付款里程碑")]
[Comment("付款里程碑（固定總價預設 30/20/30/20）")]
public class QuotePaymentMilestone : IAuditable
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }
    public int Seq { get; set; }

    [Comment("期別名稱")]
    public string Name { get; set; } = string.Empty;

    [Comment("比例（%）")]
    public decimal Percent { get; set; }

    [Comment("金額（未稅）")]
    public decimal Amount { get; set; }

    [Comment("金額（含稅）")]
    public decimal AmountWithTax { get; set; }

    [Comment("請款條件")]
    public string Trigger { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

[DisplayName("報價商業條款")]
[Comment("對外報價單商業防禦條款（附錄 A 與範本專屬條款）")]
public class QuoteClause : IAuditable
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }
    public int SortOrder { get; set; }

    [Comment("條款代碼")]
    public string Code { get; set; } = string.Empty;

    [Comment("條款標題")]
    public string Title { get; set; } = string.Empty;

    [Comment("條款內容")]
    public string Body { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

[DisplayName("報價 SLA 分級")]
[Comment("維運報價 SLA 分級：首次回應與目標解決時間")]
public class QuoteSlaLevel : IAuditable
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }

    [Comment("等級（P1／P2／P3）")]
    public string Level { get; set; } = string.Empty;

    [Comment("定義說明")]
    public string Definition { get; set; } = string.Empty;

    [Comment("首次回應時間")]
    public string ResponseTime { get; set; } = string.Empty;

    [Comment("目標解決／緩解時間")]
    public string ResolveTarget { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

// =====================================================================
// 審批（SOP 5.2）與稽核軌跡（SOP 6.1 approval_logs）
// =====================================================================

/// <summary>審批關卡：送審時依燈號與 IP 條款產生，依序決定；任一關卡退件即整輪結束。</summary>
[DisplayName("報價審批關卡")]
[Comment("審批關卡：輪次、順序、角色與決定（含送審內容雜湊）")]
public class ApprovalStep : IAuditable
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }
    public int Cycle { get; set; }
    public int Seq { get; set; }
    public ApprovalRole Role { get; set; }
    public ApprovalStepState State { get; set; } = ApprovalStepState.Pending;
    public Guid? DecidedBy { get; set; }

    [Comment("決定者姓名")]
    public string? DecidedByName { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    [Comment("審批意見")]
    public string? Comment { get; set; }

    [Comment("決定當下比對之內容雜湊")]
    public string? ContentHash { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public QuoteVersion? QuoteVersion { get; set; }
}

/// <summary>報價稽核軌跡（只增不改）：所有簽核關卡與生命週期動作、操作者、IP 與時間戳記。</summary>
[DisplayName("報價稽核軌跡")]
[Comment("報價稽核軌跡：動作、操作者 ID、IP、時間戳記（只增不改）")]
public class ApprovalLog
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }
    public QuoteLogAction Action { get; set; }
    public ApprovalRole? Role { get; set; }
    public int Cycle { get; set; }
    public Guid ActorUserId { get; set; }

    [Comment("操作者名稱")]
    public string ActorName { get; set; } = string.Empty;

    [Comment("來源 IP 位址")]
    public string? IpAddress { get; set; }

    [Comment("時間戳記（UTC）")]
    public DateTimeOffset At { get; set; }

    [Comment("說明／意見")]
    public string? Message { get; set; }

    [Comment("當下內容雜湊")]
    public string? ContentHash { get; set; }
}

[DisplayName("報價附件")]
[Comment("報價附件（檔案存放於 FMS，僅存 FileId 索引）")]
public class QuoteAttachment : IAuditable
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }
    public Guid FileId { get; set; }
    public QuoteAttachmentKind Kind { get; set; }

    [Comment("系統檔名")]
    public string FileName { get; set; } = string.Empty;

    [Comment("原始檔名")]
    public string OriginalFileName { get; set; } = string.Empty;

    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

// =====================================================================
// CR 變更影響評估（SOP 4.3 / 附錄 B）
// =====================================================================

[DisplayName("CR 變更評估單")]
[Comment("專案變更影響評估單：差異、量化工時、追加金額、時程衝擊")]
public class ChangeAssessment : IAuditable, IConcurrencyAware
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }

    [Comment("評估單號 {報價單號}-CR{NN}")]
    public string AssessmentNo { get; set; } = string.Empty;

    [Comment("原報價版本（須為已成交版本）")]
    public Guid BaseQuoteVersionId { get; set; }

    [Comment("衍生之 CR 報價版本")]
    public Guid? CrQuoteVersionId { get; set; }

    public ChangeRequester Requester { get; set; }

    [Comment("提出人")]
    public string RequesterName { get; set; } = string.Empty;

    [Comment("收到請求日")]
    public DateOnly ReceivedDate { get; set; }

    [Comment("評估期限（收到後 3 個工作日）")]
    public DateOnly AssessmentDueDate { get; set; }

    [Comment("變更標題")]
    public string Title { get; set; } = string.Empty;

    [Comment("原規格定義")]
    public string OriginalSpec { get; set; } = string.Empty;

    [Comment("變更後需求")]
    public string ChangedSpec { get; set; } = string.Empty;

    [Comment("新增開發工時（人天）")]
    public decimal NewDevDays { get; set; }

    [Comment("既有重工工時（人天）")]
    public decimal ReworkDays { get; set; }

    [Comment("回歸測試工時（人天）")]
    public decimal RegressionDays { get; set; }

    [Comment("專案管理工時（人天）")]
    public decimal PmDays { get; set; }

    [Comment("追加金額（未稅；取自 CR 報價版本）")]
    public decimal AmountUntaxed { get; set; }

    [Comment("追加金額（含稅）")]
    public decimal AmountWithTax { get; set; }

    [Comment("對 UAT／上線日之順延工作日數")]
    public int DelayWorkdays { get; set; }

    public ChangeAssessmentStatus Status { get; set; } = ChangeAssessmentStatus.Assessing;

    [Comment("客戶授權代表人")]
    public string? CustomerSignatory { get; set; }

    public DateOnly? CustomerSignedDate { get; set; }

    public byte[] RowVersion { get; set; } = [];
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

// =====================================================================
// 結案復盤（SOP 第七章）
// =====================================================================

[DisplayName("結案工時復盤")]
[Comment("預估與實際工時對比報告：結案後 14 個工作日內產出")]
public class VarianceReview : IAuditable, IConcurrencyAware
{
    public Guid Id { get; set; }

    [Comment("比對之已成交報價版本")]
    public Guid QuoteVersionId { get; set; }

    [Comment("專案結案日")]
    public DateOnly ClosedDate { get; set; }

    [Comment("報告期限（結案後 14 個工作日）")]
    public DateOnly ReportDueDate { get; set; }

    public VarianceReviewStatus Status { get; set; } = VarianceReviewStatus.Open;

    [Comment("復盤會議結論")]
    public string? RetrospectiveNotes { get; set; }

    [Comment("模組庫校準期限（復盤後 7 日）")]
    public DateOnly? CalibrationDueDate { get; set; }

    public byte[] RowVersion { get; set; } = [];
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public List<VarianceItem> Items { get; set; } = [];
}

[DisplayName("工時偏差項目")]
[Comment("WBS 項目預估 vs 實際工時與偏差率（±15% 須復盤校準）")]
public class VarianceItem : IAuditable
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public Guid? WbsItemId { get; set; }
    public Guid? TemplateItemId { get; set; }

    [Comment("WBS 編號")]
    public string WbsCode { get; set; } = string.Empty;

    [Comment("項目名稱")]
    public string Name { get; set; } = string.Empty;

    [Comment("報價預估人天")]
    public decimal QuotedDays { get; set; }

    [Comment("實際消耗人天")]
    public decimal? ActualDays { get; set; }

    [Comment("偏差率（實際－預估）／預估")]
    public decimal? VarianceRate { get; set; }

    [Comment("偏差超過 ±15%")]
    public bool ExceedsThreshold { get; set; }

    [Comment("偏差原因")]
    public string? RootCause { get; set; }

    [Comment("已校準標準工時庫")]
    public bool Calibrated { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public VarianceReview? Review { get; set; }
}
