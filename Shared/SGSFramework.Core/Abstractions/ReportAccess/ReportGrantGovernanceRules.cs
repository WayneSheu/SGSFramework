#nullable enable

namespace SGSFramework.Core.Abstractions.ReportAccess
{
    using SGSFramework.Core.Abstractions.Entities.ReportCategories;

    /// <summary>
    /// 報告授權治理規則：判定哪些授權須經雙重審批（Maker-Checker）方可生效。
    /// </summary>
    public static class ReportGrantGovernanceRules
    {
        /// <summary>具高風險之動作：簽署、作廢、檢視盲樣識別資訊</summary>
        public const ReportActions SensitiveActions = ReportActions.Sign | ReportActions.Void | ReportActions.ViewBlindIdentity;

        /// <summary>
        /// 敏感授權：含高風險動作、跨分公司全域範圍，或可存取特檢密級。
        /// </summary>
        public static bool RequiresApproval(ReportActions actions, ReportDataScope dataScope, ConfidentialityLevel maxConfidentiality) =>
            (actions & SensitiveActions) != 0 ||
            dataScope == ReportDataScope.Global ||
            maxConfidentiality == ConfidentialityLevel.Restricted;
    }
}
