using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Modules;

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

        return names.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
