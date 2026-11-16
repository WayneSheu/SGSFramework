using System.Text.Json.Serialization;

namespace SGS.Modules.QTN.Infrastructure.Entities;

/// <summary>計價模式（SOP 第三章 / 6.1 projects.pricing_model）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingModel : byte
{
    /// <summary>固定總價（Fixed Price）。</summary>
    FixedPrice = 0,

    /// <summary>工時與材料費（T&amp;M 雙週衝刺產能包）。</summary>
    TimeAndMaterials = 1,

    /// <summary>混合計價（前置健檢固定總價 + 開發期 T&amp;M）。</summary>
    Hybrid = 2,

    /// <summary>固定月費維運（SLA Retainer）。</summary>
    Retainer = 3
}

/// <summary>專案類型（SOP 第三章策略矩陣）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectCategory : byte
{
    /// <summary>標準型客製專案。</summary>
    StandardCustom = 0,

    /// <summary>創新型／敏捷專案。</summary>
    Innovative = 1,

    /// <summary>系統重構／遺留遷移。</summary>
    Refactor = 2,

    /// <summary>長期維運與駐點。</summary>
    Maintenance = 3
}

/// <summary>商機狀態。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OpportunityStatus : byte
{
    Open = 0,
    Won = 1,
    Lost = 2,
    Cancelled = 3
}

/// <summary>商機成員角色（資料列授權依據；非成員且非審批人一律 404）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OpportunityRole : byte
{
    Sales = 0,
    TechLead = 1,
    Pm = 2,
    Finance = 3
}

/// <summary>WBS 階段（SOP 4.1 標準工時結構）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WbsPhase : byte
{
    /// <summary>需求訪談與架構設計（15%–20%）。</summary>
    Analysis = 0,

    /// <summary>核心功能開發（50%–60%）。</summary>
    Development = 1,

    /// <summary>品質保證與測試（15%–20%）。</summary>
    QualityAssurance = 2,

    /// <summary>專案管理與部署（10%–15%）。</summary>
    Management = 3
}

/// <summary>報價版本狀態機（SOP 6.1 quote_versions.status，另含對外發出與成交結果）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuoteStatus : byte
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Superseded = 4,

    /// <summary>已對外發出（報價有效期起算）。</summary>
    Issued = 5,

    /// <summary>客戶簽約（得標）。</summary>
    Won = 6,

    /// <summary>未得標。</summary>
    Lost = 7,

    /// <summary>逾報價有效期未成交。</summary>
    Expired = 8
}

/// <summary>毛利率紅綠燈（SOP 5.2）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MarginLight : byte
{
    Green = 0,
    Yellow = 1,
    Red = 2
}

/// <summary>審批關卡角色（SOP 5.2 審批權責矩陣）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApprovalRole : byte
{
    TechLead = 0,
    SalesManager = 1,
    PmoDirector = 2,
    Cfo = 3,
    Ceo = 4,

    /// <summary>法務長（IP 買斷強制會簽）。</summary>
    Legal = 5
}

/// <summary>審批關卡狀態。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApprovalStepState : byte
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,

    /// <summary>同輪其他關卡已退件，本關卡不再需要決定。</summary>
    Cancelled = 3
}

/// <summary>稽核軌跡動作（SOP 6.1 approval_logs）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuoteLogAction : byte
{
    Created = 0,
    Updated = 1,
    TechReviewed = 2,
    Submitted = 3,
    Approved = 4,
    Rejected = 5,
    SpecialApprovalRequested = 6,
    FullyApproved = 7,
    Issued = 8,
    Won = 9,
    Lost = 10,
    Expired = 11,
    Revised = 12,
    Superseded = 13,
    ChangeRequestCreated = 14,
    ConvertedToContract = 15,
    AttachmentAdded = 16,
    AttachmentRemoved = 17,
    PdfExported = 18
}

/// <summary>專案直接費用類別（SOP 5.1）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExpenseCategory : byte
{
    /// <summary>雲端測試環境租賃費。</summary>
    Cloud = 0,

    /// <summary>專屬第三方授權費。</summary>
    License = 1,

    /// <summary>駐點差旅費。</summary>
    Travel = 2,

    Other = 9
}

/// <summary>報價附件類型。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuoteAttachmentKind : byte
{
    /// <summary>初步需求規格清單等客戶提供資料。</summary>
    Requirement = 0,

    /// <summary>折扣說明書（黃燈以上必附）。</summary>
    DiscountExplanation = 1,

    /// <summary>客戶簽回之報價單／CR 追加協議（證據文件，不可刪除）。</summary>
    CustomerSigned = 2,

    Other = 9
}

/// <summary>CR 變更提出方。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChangeRequester : byte
{
    Customer = 0,
    Internal = 1
}

/// <summary>CR 變更評估單狀態。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChangeAssessmentStatus : byte
{
    /// <summary>評估中（3 個工作日內完成）。</summary>
    Assessing = 0,

    /// <summary>已評估並衍生 CR 報價版本，待內部審批與客戶簽署。</summary>
    Assessed = 1,

    /// <summary>客戶已書面簽署追加費用，可排入開發排程。</summary>
    CustomerSigned = 2,

    Cancelled = 3
}

/// <summary>結案復盤狀態（SOP 第七章）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VarianceReviewStatus : byte
{
    /// <summary>待填實際工時（結案後 14 個工作日內）。</summary>
    Open = 0,

    /// <summary>已產出偏差報告，有超標項目待復盤會議。</summary>
    PendingRetrospective = 1,

    /// <summary>已完成（超標項目皆已記錄原因並校準模組庫）。</summary>
    Closed = 2
}
