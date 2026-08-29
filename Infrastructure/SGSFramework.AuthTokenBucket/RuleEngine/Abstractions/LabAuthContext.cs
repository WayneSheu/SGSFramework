using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.AuthTokenBucket.RuleEngine.Abstractions
{
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

    }
}
