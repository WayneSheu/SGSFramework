#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Core.Abstractions.Strategies;

/// <summary>
/// 實驗室權限指派策略介面
/// </summary>
public interface ILabPermissionStrategy
{
    /// <summary>
    /// 判定策略是否適用於此指派情境 (主區域實驗室為 true，兼任實驗室為 false)
    /// </summary>
    bool AppliesTo(bool isPrimary);

    /// <summary>
    /// 依據申請的遮罩，結合動態中繼資料進行權限過濾與限縮
    /// </summary>
    Task<long> ValidateAndFilterMaskAsync(long requestedMask, CancellationToken cancellationToken = default);
}