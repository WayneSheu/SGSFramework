using SGSFramework.AuthTokenBucket.RuleEngine.Abstractions;
using SGSFramework.Core.Abstractions.ReportAccess;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.RuleEngine.Rules.Laboratory
{
    /// <summary>
    /// 報告類別規則：目標實驗室須承作任一指定報告類別，且使用者具該類別之指定動作授權
    /// （報告類別主檔 + 類別授權，取代以實驗室代碼比對類別的舊行為）。
    /// </summary>
    public sealed class ReportCategoryRule : ILabAuthorizationRule
    {
        private readonly IReportAccessAuthorizationService _reportAccessService;

        public int ExecutionOrder => 20;

        public ReportCategoryRule(IReportAccessAuthorizationService reportAccessService)
        {
            _reportAccessService = reportAccessService ?? throw new ArgumentNullException(nameof(reportAccessService));
        }

        public async Task<(bool IsPassed, string ErrorMessage)> EvaluateAsync(LabAuthContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Kind != LabAuthRuleKind.ReportCategory ||
                context.AllowedCategories is null || context.AllowedCategories.Length == 0)
            {
                return (true, string.Empty);
            }

            bool isAllowed = await _reportAccessService.HasCategoryActionAsync(
                context.User, context.TargetLabId, context.AllowedCategories, context.RequiredAction, cancellationToken);

            return isAllowed
                ? (true, string.Empty)
                : (false, "不具此報告類別於目前實驗室之授權，或該實驗室未承作此類別。");
        }
    }
}
