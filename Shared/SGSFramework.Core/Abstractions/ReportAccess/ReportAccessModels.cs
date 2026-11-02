#nullable enable

namespace SGSFramework.Core.Abstractions.ReportAccess
{
    using SGSFramework.Core.Abstractions.Entities.ReportCategories;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 受檢核之報告資料上下文（由業務模組自報告實體組成）
    /// </summary>
    public sealed record ReportAccessContext
    {
        public required string CategoryCode { get; init; }

        /// <summary>報告所屬區域實驗室（分公司）</summary>
        public required Guid TenantLabId { get; init; }

        public ReportLifecycleStatus Status { get; init; } = ReportLifecycleStatus.Draft;

        public ConfidentialityLevel Confidentiality { get; init; } = ConfidentialityLevel.Normal;

        public bool IsBlind { get; init; }

        /// <summary>被指派（經手）此報告之使用者（分析員、審核員、簽署人、專案成員、負責業務）</summary>
        public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];

        /// <summary>報告作者（數據登錄者），用於職能分立檢核</summary>
        public Guid? AuthorId { get; init; }

        /// <summary>報告審核者，用於職能分立檢核</summary>
        public Guid? ReviewerId { get; init; }
    }

    /// <summary>
    /// 使用者於某區域實驗室下之有效報告類別授權（使用者授權與有效角色授權之聯集項目）
    /// </summary>
    public sealed record EffectiveReportGrant(
        string CategoryCode,
        Guid? TenantLabId,
        ReportActions Actions,
        ReportDataScope DataScope,
        ConfidentialityLevel MaxConfidentiality);

    /// <summary>
    /// 授權判斷所需之使用者快照（以「使用者 × 區域實驗室」為單位計算並快取）
    /// </summary>
    public sealed record ReportAccessSubject
    {
        public required Guid UserId { get; init; }

        /// <summary>快照計算所依據的區域實驗室</summary>
        public required Guid TenantLabId { get; init; }

        public bool IsSuperAdmin { get; init; }

        /// <summary>使用者於此實驗室具有效歸屬（UserLabMapping）</summary>
        public bool IsLabMember { get; init; }

        /// <summary>此實驗室可承作的報告類別代碼</summary>
        public IReadOnlyCollection<string> LabCategoryCodes { get; init; } = [];

        public IReadOnlyList<EffectiveReportGrant> Grants { get; init; } = [];

        /// <summary>使用者於此實驗室有效之簽署資格類別代碼</summary>
        public IReadOnlyCollection<string> SignerCategoryCodes { get; init; } = [];
    }

    /// <summary>
    /// 授權判斷結果
    /// </summary>
    public sealed record ReportAccessDecision(bool IsAllowed, string Reason, bool CanSeeBlindIdentity)
    {
        public static ReportAccessDecision Deny(string reason) => new(false, reason, false);
    }
}
