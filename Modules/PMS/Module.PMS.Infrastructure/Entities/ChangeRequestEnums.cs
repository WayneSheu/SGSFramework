using System.Text.Json.Serialization;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>CR 分類（SOP 4.2）：客戶 CR 報價並需客戶簽核；內部 CR 不報價。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrKind
{
    Customer = 0,
    Internal = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrStatus
{
    /// <summary>已受理，待 Tech Lead 影響評估。</summary>
    Received = 0,

    /// <summary>已評估，待 PM 決策。</summary>
    PendingDecision = 1,

    /// <summary>已決策（A 順延／B 範圍交換），待簽核。</summary>
    PendingSign = 2,

    Approved = 3,

    Rejected = 4,

    /// <summary>暫緩：轉入 Backlog 排入下期規劃。</summary>
    Deferred = 5,

    Cancelled = 6
}

/// <summary>決策方案（SOP 4.4）：A 時程順延、B 範圍交換、C 駁回或暫緩。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrDecision
{
    A_Delay = 0,
    B_Swap = 1,
    C_Reject = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrApiImpact
{
    None = 0,
    Compatible = 1,

    /// <summary>破壞性變更：前後端需重新聯調（REL-05）。</summary>
    Breaking = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrCouplingImpact
{
    Isolated = 0,
    ContractChange = 1,

    /// <summary>架構警告：將造成循環依賴，需額外重構工時。</summary>
    ArchitectureWarning = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrSignRole
{
    Pm = 1,
    TechLead = 2,
    Customer = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrEventType
{
    Created = 0,
    Evaluated = 1,
    Decided = 2,
    Signed = 3,
    Approved = 4,
    Cancelled = 5,
    Reopened = 6
}
