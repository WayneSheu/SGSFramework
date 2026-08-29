using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.RuleEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.RuleEngine.Rules.Laboratory
{
    /// <summary>
    /// 實驗室規則引擎實例
    /// </summary>
    public sealed class LaboratoryRuleEngine
    {
        private readonly ILogger<LaboratoryRuleEngine> _logger;
        private readonly IEnumerable<ILabAuthorizationRule> _rules;

        public LaboratoryRuleEngine(
            ILogger<LaboratoryRuleEngine> logger,
            IEnumerable<ILabAuthorizationRule> rules)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rules = rules?.OrderBy(r => r.ExecutionOrder) ?? Enumerable.Empty<ILabAuthorizationRule>();
        }

        /// <summary>
        /// 執行實驗室規則引擎
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<(bool IsPassed, string ErrorMessage)> ExecuteAsync(LabAuthContext context, CancellationToken cancellationToken)
        {
            foreach (var rule in _rules)
            {
                try
                {
                    var (passed, message) = await rule.EvaluateAsync(context, cancellationToken);
                    if (!passed)
                    {
                        _logger.LogWarning("[RuleEngine] 規則檢查未通過: {RuleName}, 原因: {Message}", rule.GetType().Name, message);
                        return (false, message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RuleEngine] 執行授權規則 {RuleName} 時發生例外", rule.GetType().Name);
                    return (false, "授權檢核程序發生內部異常。");
                }
            }

            return (true, string.Empty);
        }
    }
}
