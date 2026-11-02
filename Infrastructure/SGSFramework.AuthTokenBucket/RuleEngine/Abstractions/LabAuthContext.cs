using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.AuthTokenBucket.RuleEngine.Abstractions
{
    /// <summary>
    /// 規則引擎檢核種類：決定 <see cref="LabAuthContext.AllowedCategories"/> 代表實驗室類別或報告類別。
    /// </summary>
    public enum LabAuthRuleKind
    {
        /// <summary>實驗室類別（RequireLaboratory）</summary>
        LaboratoryCategory = 0,

        /// <summary>報告類別（RequireReportCategory）</summary>
        ReportCategory = 1
    }

    /// <summary>
    /// 實驗室身份驗證請求的上下文。此類別包含有關使用者、使用者 ID、目標實驗室 ID 以及實驗室允許類別的資訊。
    public sealed class LabAuthContext
    {

        public required ClaimsPrincipal User { get; init; }

        public required Guid UserId { get; init; }

        /// <summary>
        /// 目標實驗室 ID。
        /// </summary>
        public required Guid TargetLabId { get; init; }
        /// <summary>
        /// 允許的實驗室類別。如果未指定，則表示允許所有類別。
        /// </summary>
        public string[] AllowedCategories { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 檢核種類（實驗室類別 / 報告類別）。
        /// </summary>
        public LabAuthRuleKind Kind { get; init; } = LabAuthRuleKind.LaboratoryCategory;

        /// <summary>
        /// 報告類別檢核所需之動作（僅 <see cref="LabAuthRuleKind.ReportCategory"/> 使用）。
        /// </summary>
        public ReportActions RequiredAction { get; init; } = ReportActions.View;

    }
}
