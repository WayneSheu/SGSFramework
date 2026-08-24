using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.Identity.Repositories;

/// <summary>
/// 使用者與實驗室關聯資料存取實作 (支援多租戶隔離與兼任/主要實驗室切換)
/// </summary>
public class UserLabRepository : IUserLabRepository
{
    private readonly DbContext _context;

    public UserLabRepository(DbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// 取得使用者目前啟用的主要實驗室對應
    /// </summary>
    public async Task<UserLabMapping?> GetPrimaryLabAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return null;

        return await _context.Set<UserLabMapping>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsPrimary && x.IsActive, cancellationToken);
    }

    /// <summary>
    /// 取得使用者可存取且位於生效時效內的所有實驗室清單
    /// </summary>
    public async Task<List<UserLabMapping>> GetAccessibleLabsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return new List<UserLabMapping>();

        var now = DateTime.UtcNow;

        return await _context.Set<UserLabMapping>()
            .AsNoTracking()
            .Where(x => x.UserId == userId
                     && x.IsActive
                     && x.EffectiveDate <= now
                     && (x.ExpiryDate == null || x.ExpiryDate >= now))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 切換使用者的主要實驗室 (自動將舊 Primary 降級為 Secondary)
    /// </summary>
    public async Task SetPrimaryLabAsync(Guid userId, int newPrimaryLabId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId 不能為 Empty Guid。", nameof(userId));
        if (newPrimaryLabId <= 0) throw new ArgumentOutOfRangeException(nameof(newPrimaryLabId), "LabId 必須大於 0。");

        try
        {
            var userLabs = await _context.Set<UserLabMapping>()
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync(cancellationToken);

            var targetLab = userLabs.FirstOrDefault(x => x.LabId == newPrimaryLabId);
            if (targetLab is null)
            {
                throw new InvalidOperationException($"目標實驗室 (ID: {newPrimaryLabId}) 不在該使用者的可存取權限範圍內。");
            }

            // 1. 降級既有的 Primary 記錄
            var currentPrimary = userLabs.FirstOrDefault(x => x.IsPrimary);
            if (currentPrimary is not null && currentPrimary.LabId != newPrimaryLabId)
            {
                currentPrimary.DemoteToSecondary();
            }

            // 2. 升級目標實驗室為 Primary
            targetLab.PromoteToPrimary();

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("設定主要實驗室失敗，可能違反資料庫唯一性限制。", ex);
        }
    }

    /// <summary>
    /// 新增或更新使用者實驗室關聯 (含自動處理 Primary 衝突降級防禦)
    /// </summary>
    public async Task AddOrUpdateSecondaryLabAsync(UserLabMapping mapping, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping, nameof(mapping));

        try
        {
            // 若新新增/更新的紀錄標記為 IsPrimary，先處理既有 Primary 降級，避免違反 UX 唯一索引
            if (mapping.IsPrimary)
            {
                var existingPrimary = await _context.Set<UserLabMapping>()
                    .FirstOrDefaultAsync(x => x.UserId == mapping.UserId && x.IsPrimary && x.IsActive && x.LabId != mapping.LabId, cancellationToken);

                existingPrimary?.DemoteToSecondary();
            }

            var existing = await _context.Set<UserLabMapping>()
                .FirstOrDefaultAsync(x => x.UserId == mapping.UserId && x.LabId == mapping.LabId, cancellationToken);

            if (existing is null)
            {
                await _context.Set<UserLabMapping>().AddAsync(mapping, cancellationToken);
            }
            else
            {
                // 更新現有對應實體狀態與資料
                _context.Entry(existing).CurrentValues.SetValues(mapping);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("儲存使用者實驗室對應資料失敗，違反資料庫約束限制。", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("儲存使用者實驗室對應資料時發生未知錯誤。", ex);
        }
    }
}