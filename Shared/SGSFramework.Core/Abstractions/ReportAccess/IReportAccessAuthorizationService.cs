#nullable enable

namespace SGSFramework.Core.Abstractions.ReportAccess
{
    using SGSFramework.Core.Abstractions.Entities.ReportCategories;
    using System;
    using System.Collections.Generic;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// 報告資料層授權服務（功能層權限仍由 Controller 上的 [RequiresPermission] 負責）。
    /// </summary>
    public interface IReportAccessAuthorizationService
    {
        /// <summary>檢核使用者對指定報告是否可執行單一動作</summary>
        Task<ReportAccessDecision> AuthorizeAsync(
            ClaimsPrincipal user,
            ReportAccessContext context,
            ReportActions action,
            CancellationToken cancellationToken = default);

        /// <summary>取得使用者於指定區域實驗室之授權快照（供業務模組組成查詢範圍條件）</summary>
        Task<ReportAccessSubject?> GetSubjectAsync(
            ClaimsPrincipal user,
            Guid tenantLabId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 類別層級檢核（無特定報告時，例如清單端點）：使用者於該實驗室是否對任一指定類別具有指定動作之授權，
        /// 且該實驗室承作此類別。
        /// </summary>
        Task<bool> HasCategoryActionAsync(
            ClaimsPrincipal user,
            Guid tenantLabId,
            IReadOnlyCollection<string> categoryCodes,
            ReportActions action,
            CancellationToken cancellationToken = default);
    }
}
