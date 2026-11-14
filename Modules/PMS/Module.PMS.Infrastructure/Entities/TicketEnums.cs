using System.Text.Json.Serialization;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>問題單類型（SOP 6 分流）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketType
{
    /// <summary>操作諮詢：PM 直接回覆並補入 FAQ。</summary>
    Consultation = 0,

    /// <summary>需求變更：走 CR 流程（SOP 第 4 章）。</summary>
    ChangeRequest = 1,

    /// <summary>系統異常：依 P1／P2／P3 分級。</summary>
    Incident = 2,

    /// <summary>資料撈取：視合約計費。</summary>
    DataRequest = 3
}

/// <summary>異常優先級（SOP 6.3）。僅「系統異常」需要。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketPriority
{
    P1 = 1,
    P2 = 2,
    P3 = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketStatus
{
    /// <summary>已受理，待 PM 分流。</summary>
    Received = 0,

    /// <summary>處理中。</summary>
    InProgress = 1,

    /// <summary>修復者已回報，待非作者驗證。</summary>
    PendingVerify = 2,

    /// <summary>驗證通過，待客戶複測與 PM 結案。</summary>
    PendingRetest = 3,

    Closed = 4,

    Cancelled = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketSource
{
    Manual = 0,
    Line = 1,
    Email = 2,
    Stakeholder = 3
}

/// <summary>SLA 燈號（SOP 附錄 B-3）。以查詢當下時間動態計算，不持久化。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlaStatus
{
    /// <summary>不適用：CR（待報價確認）或尚未分流。</summary>
    NotApplicable = 0,

    InProgress = 1,

    /// <summary>距到期不足 2 小時。</summary>
    DueSoon = 2,

    Overdue = 3,

    MetOnTime = 4,

    MetLate = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketEventType
{
    Created = 0,
    Triaged = 1,
    Assigned = 2,
    Resolved = 3,
    Verified = 4,
    VerificationFailed = 5,
    Closed = 6,
    Cancelled = 7,
    Reopened = 8,
    AttachmentAdded = 9,
    Comment = 10
}
