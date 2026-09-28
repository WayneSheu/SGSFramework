

#nullable enable

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.Permissions.Repositories;
using SGSFramework.Core.Abstractions.Strategies;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Application.Strategies;

/// <summary>
/// 主區域實驗室權限指派策略 (基於動態中繼資料)
/// 核心業務規則：主區域實驗室負責人/管理員允許被賦予所有敏感級別的操作權限 (包含 Critical 與 Administrative)。
/// 此策略主要確保所申請的位元遮罩皆為系統中合法註冊的有效權限，避免注入未定義的幽靈權限。
/// </summary>
public sealed class PrimaryLabPermissionStrategy : ILabPermissionStrategy
{
    private readonly IPermissionMetadataRepository _metadataRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PrimaryLabPermissionStrategy> _logger;

    private const string MetadataCacheKey = "Sys_PermissionMetadata_All";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public PrimaryLabPermissionStrategy(
        IPermissionMetadataRepository metadataRepository,
        IMemoryCache cache,
        ILogger<PrimaryLabPermissionStrategy> logger)
    {
        _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool AppliesTo(bool isPrimary) => isPrimary;

    public async Task<long> ValidateAndFilterMaskAsync(long requestedMask, CancellationToken cancellationToken = default)
    {
        // 快速通關：若未請求任何權限，直接回傳空遮罩
        if (requestedMask == 0)
        {
            return 0L;
        }

        try
        {
            // 優先由記憶體快取讀取全域權限元資料，降低資料庫負載
            if (!_cache.TryGetValue(MetadataCacheKey, out IReadOnlyCollection<PermissionMetadata>? metadataList) || metadataList == null)
            {
                metadataList = await _metadataRepository.GetAllMetadataAsync(cancellationToken)
                    .ConfigureAwait(false);

                _cache.Set(MetadataCacheKey, metadataList, CacheDuration);
            }

            if (metadataList.Count == 0)
            {
                _logger.LogWarning("[PrimaryLabPermissionStrategy] 無法取得系統權限中繼資料，為確保安全性，將拒絕所有權限請求。");
                return 0L;
            }

            long finalMask = 0L;

            // 主區域實驗室允許所有 ActionCategory (Basic, Operational, Critical, Administrative)，
            // 故不針對 Category 進行黑名單過濾，僅需確認該 BitPosition 真實存在於 Metadata 中。
            foreach (var metadata in metadataList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 防禦性檢查：忽略無效的位元位置，防止越界異常與記憶體錯誤
                if (metadata.BitPosition is < 0 or > 63)
                {
                    continue;
                }

                long currentBit = 1L << metadata.BitPosition;

                // 檢查使用者申請的遮罩是否包含此合法的系統功能位元
                if ((requestedMask & currentBit) == currentBit)
                {
                    finalMask |= currentBit;
                }
            }

            // 紀錄若有被剔除的非法權限請求 (申請了系統未定義的 Bit)
            if (finalMask != requestedMask)
            {
                _logger.LogWarning("[PrimaryLabPermissionStrategy] 偵測到無效的權限申請位元。原始請求遮罩: {RequestedMask}，過濾後核准遮罩: {FinalMask}",
                    requestedMask, finalMask);
            }

            return finalMask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PrimaryLabPermissionStrategy] 權限遮罩驗證與過濾作業已取消。");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PrimaryLabPermissionStrategy] 主區域實驗室權限驗證過程發生未預期的系統異常。");
            throw;
        }
    }
}

