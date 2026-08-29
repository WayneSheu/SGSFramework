using SGSFramework.AuthTokenBucket.RuleEngine.Abstractions;
using SGSFramework.Core.Abstractions.Permissions.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.RuleEngine.Rules.Laboratory
{
    public sealed class LaboratoryCategoryRule : ILabAuthorizationRule
    {
        private readonly ILaboratoryAccessService _labAccessService;

        public int ExecutionOrder => 10;

        public LaboratoryCategoryRule(ILaboratoryAccessService labAccessService)
        {
            _labAccessService = labAccessService ?? throw new ArgumentNullException(nameof(labAccessService));
        }

        public async Task<(bool IsPassed, string ErrorMessage)> EvaluateAsync(LabAuthContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.AllowedCategories is null || context.AllowedCategories.Length == 0)
            {
                return (true, string.Empty);
            }

            // 1. 驗證使用者是否具備該實驗室的基本存取權限 (底層整合 UserLabMapping 驗證)[cite: 8]
            bool hasAccess = await _labAccessService.ValidateUserLaboratoryAccessAsync(context.UserId, context.TargetLabId, cancellationToken);
            if (!hasAccess)
            {
                return (false, "使用者無權存取指定的實驗室上下文。");
            }

            // 2. 驗證實驗室類別是否符合規則限制
            bool isValidCategory = await _labAccessService.ValidateLaboratoryCategoryAsync(context.TargetLabId, context.AllowedCategories, cancellationToken);
            if (!isValidCategory)
            {
                return (false, $"實驗室類別未符合當前功能操作的授權要求範圍。");
            }

            return (true, string.Empty);
        }
    }
}
