using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.RuleEngine.Abstractions
{
    /// <summary>
    /// 
    /// </summary>
    public interface ILabAuthorizationRule
    {
        /// <summary>
        /// 執行順序
        /// </summary>
        int ExecutionOrder { get; }

        /// <summary>
        /// 進行驗證
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<(bool IsPassed, string ErrorMessage)> EvaluateAsync(LabAuthContext context, CancellationToken cancellationToken);
    
    }
}
