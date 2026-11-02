#nullable enable

namespace SGSFramework.Core.Abstractions.ReportAccess
{
    using SGSFramework.Core.Abstractions.Entities.ReportCategories;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// 報告存取授權政策（純函式，無 I/O）。依序檢核，任一不通過即拒絕：
    /// 1. 狀態 × 動作矩陣　2. 職能分立（作者 ≠ 審核者 ≠ 簽署人）　3. 簽署資格
    /// 4. 承作實驗室　5. 特檢密級須明確指派　6. 類別授權（動作、資料範圍、最高密級）　7. 草稿僅限經手人與主管
    /// 第 1～4 項屬資料完整性規則，最高管理者亦須遵守。
    /// </summary>
    public static class ReportAccessPolicy
    {
        /// <summary>需由承作實驗室執行之生產動作</summary>
        private const ReportActions ProductionActions = ReportActions.Edit | ReportActions.Review | ReportActions.Sign;

        public static ReportAccessDecision Evaluate(ReportAccessSubject subject, ReportAccessContext context, ReportActions action)
        {
            ArgumentNullException.ThrowIfNull(subject);
            ArgumentNullException.ThrowIfNull(context);

            if (BitOperations.PopCount((uint)action) != 1)
            {
                throw new ArgumentException("一次僅能檢核單一動作。", nameof(action));
            }

            if (subject.TenantLabId != context.TenantLabId)
            {
                throw new ArgumentException("授權快照之區域實驗室與報告所屬實驗室不一致。", nameof(subject));
            }

            if (!ReportStatusActionMatrix.Allows(context.Status, action))
            {
                return ReportAccessDecision.Deny("目前報告狀態不允許此操作。");
            }

            var userId = subject.UserId;

            if (action == ReportActions.Review && context.AuthorId == userId)
            {
                return ReportAccessDecision.Deny("職能分立：報告作者不得審核自己的報告。");
            }

            if (action == ReportActions.Sign && (context.AuthorId == userId || context.ReviewerId == userId))
            {
                return ReportAccessDecision.Deny("職能分立：報告作者或審核者不得簽署同一份報告。");
            }

            if (action == ReportActions.Sign && !subject.SignerCategoryCodes.Contains(context.CategoryCode, StringComparer.OrdinalIgnoreCase))
            {
                return ReportAccessDecision.Deny("不具此類別於本實驗室之有效簽署人資格。");
            }

            if ((action & ProductionActions) != 0 &&
                !subject.LabCategoryCodes.Contains(context.CategoryCode, StringComparer.OrdinalIgnoreCase))
            {
                return ReportAccessDecision.Deny("此區域實驗室未承作該報告類別。");
            }

            if (subject.IsSuperAdmin)
            {
                return new ReportAccessDecision(true, string.Empty, CanSeeBlindIdentity: true);
            }

            bool isAssigned = context.AssignedUserIds.Contains(userId);

            if (context.Confidentiality == ConfidentialityLevel.Restricted && !isAssigned)
            {
                return ReportAccessDecision.Deny("特檢 / 高度機密報告僅限明確指派之人員存取。");
            }

            var matched = MatchGrants(subject, context, action, isAssigned).ToList();
            if (matched.Count == 0)
            {
                return ReportAccessDecision.Deny("不具此報告類別、實驗室或密級之授權。");
            }

            // 草稿階段：除經手人外，僅主管級（具作廢授權）可檢視
            if (context.Status == ReportLifecycleStatus.Draft && action == ReportActions.View && !isAssigned &&
                !matched.Any(g => g.Actions.HasFlag(ReportActions.Void)))
            {
                return ReportAccessDecision.Deny("草稿階段僅限經手人員與實驗室主管檢視。");
            }

            bool canSeeBlindIdentity = !context.IsBlind ||
                MatchGrants(subject, context, ReportActions.ViewBlindIdentity, isAssigned).Any();

            return new ReportAccessDecision(true, string.Empty, canSeeBlindIdentity);
        }

        /// <summary>
        /// 類別層級檢核（無特定報告）：該實驗室承作任一指定類別，且使用者對該類別具指定動作之授權。
        /// 「僅指派」範圍於此層級視為通過，個別報告由查詢範圍與 <see cref="Evaluate"/> 再行過濾。
        /// </summary>
        public static bool HasCategoryAction(ReportAccessSubject subject, IReadOnlyCollection<string> categoryCodes, ReportActions action)
        {
            ArgumentNullException.ThrowIfNull(subject);
            ArgumentNullException.ThrowIfNull(categoryCodes);

            if (subject.IsSuperAdmin)
            {
                return true;
            }

            return categoryCodes
                .Where(code => subject.LabCategoryCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                .Any(code => subject.Grants.Any(g =>
                    string.Equals(g.CategoryCode, code, StringComparison.OrdinalIgnoreCase) &&
                    (g.TenantLabId is null || g.TenantLabId == subject.TenantLabId) &&
                    g.Actions.HasFlag(action) &&
                    (g.DataScope == ReportDataScope.Global || subject.IsLabMember)));
        }

        private static IEnumerable<EffectiveReportGrant> MatchGrants(
            ReportAccessSubject subject, ReportAccessContext context, ReportActions action, bool isAssigned) =>
            subject.Grants.Where(g =>
                string.Equals(g.CategoryCode, context.CategoryCode, StringComparison.OrdinalIgnoreCase) &&
                (g.TenantLabId is null || g.TenantLabId == context.TenantLabId) &&
                g.Actions.HasFlag(action) &&
                g.MaxConfidentiality >= context.Confidentiality &&
                g.DataScope switch
                {
                    ReportDataScope.Global => true,
                    ReportDataScope.Lab => subject.IsLabMember,
                    _ => subject.IsLabMember && isAssigned
                });
    }
}
