using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.Core.Abstractions.Permissions.Entities;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 讀取「模組管理」中已停用的模組。
/// 模組停用僅更新 ModuleMetadata.IsActive（組件仍載入於記憶體），各清單與授權檢查於讀取時依此判斷模組所屬項目是否停用，
/// 不改寫 ControllerMetadata / MenuItem / PermissionMetadata 本身的狀態（避免與手動停用、啟動同步互相覆蓋）。
/// </summary>
public interface IModuleStatusReader
{
    /// <summary>取得已停用的模組名稱 (不分大小寫)</summary>
    Task<HashSet<string>> GetInactiveModuleNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得已停用的權限鍵值與原因：使用該鍵值的系統功能 (ControllerMetadata) 全部停用 (手動停用或所屬模組停用) 時視為停用。
    /// 僅部分功能停用時權限仍可指派 (其餘功能仍需此權限)。
    /// </summary>
    Task<Dictionary<string, string>> GetDisabledPermissionKeysAsync(IReadOnlySet<string> inactiveModules, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有不可再指派的權限鍵值與原因：上述功能停用者，加上所屬模組停用的所有權限 (含無對應功能者)。
    /// 供指派 API 拒絕「新增」停用權限；既有綁定不受影響。
    /// </summary>
    Task<Dictionary<string, string>> GetUnassignablePermissionKeysAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 停用權限指派檢查的共用訊息
/// </summary>
public static class DisabledPermissionMessages
{
    public static string NewlyGranted(IEnumerable<string> keys) =>
        $"下列權限已停用（所屬模組或系統功能已停用），無法新增指派：{string.Join("、", keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))}。既有的權限綁定不受影響。";
}

public sealed class ModuleStatusReader(ITokenDbContext dbContext) : IModuleStatusReader
{
    private readonly ITokenDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<HashSet<string>> GetInactiveModuleNamesAsync(CancellationToken cancellationToken = default)
    {
        var names = await _dbContext.Set<ModuleMetadata>()
            .AsNoTracking()
            .Where(m => !m.IsActive)
            .Select(m => m.ModuleName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // 已卸載的模組（記憶體標記）視同停用：其 API 被攔截、選單隱藏、權限不可再指派
        var set = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removed in FunctionStatusRules.GetRemovedModuleNames()) set.Add(removed);
        return set;
    }

    public async Task<Dictionary<string, string>> GetDisabledPermissionKeysAsync(IReadOnlySet<string> inactiveModules, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inactiveModules);

        var rows = await _dbContext.Set<ControllerMetadata>()
            .AsNoTracking()
            .Where(c => c.PermissionKey != "")
            .Select(c => new { c.PermissionKey, c.ModuleName, c.IsManuallyDisabled })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in rows.GroupBy(r => r.PermissionKey, StringComparer.OrdinalIgnoreCase))
        {
            var byModule = group.All(r => inactiveModules.Contains(r.ModuleName));
            var allDisabled = group.All(r => r.IsManuallyDisabled || inactiveModules.Contains(r.ModuleName));
            if (allDisabled)
            {
                result[group.Key] = byModule ? "所屬模組已停用" : "系統功能已停用";
            }
        }

        return result;
    }

    public async Task<Dictionary<string, string>> GetUnassignablePermissionKeysAsync(CancellationToken cancellationToken = default)
    {
        var inactiveModules = await GetInactiveModuleNamesAsync(cancellationToken).ConfigureAwait(false);
        var result = await GetDisabledPermissionKeysAsync(inactiveModules, cancellationToken).ConfigureAwait(false);

        if (inactiveModules.Count > 0)
        {
            var moduleNames = inactiveModules.ToList();
            var moduleKeys = await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .Where(p => moduleNames.Contains(p.ModuleName) && p.PermissionKey != "")
                .Select(p => p.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var key in moduleKeys)
            {
                result[key] = "所屬模組已停用";
            }
        }

        return result;
    }
}
