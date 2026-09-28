

#nullable enable

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Abstractions.Permissions.Repositories;
using SGSFramework.Core.Abstractions.Strategies;

namespace SGSFramework.Application.Strategies;
/// <summary>
/// 兼任實驗室權限指派策略 (基於動態中繼資料)
/// 核心業務規則：限制兼任人員僅能被賦予 Basic 與 Operational 級別之操作權限，並過濾掉任何 Critical 或 Administrative 權限
/// </summary>
public sealed class AdjunctLabPermissionStrategy : ILabPermissionStrategy
{
    private readonly IPermissionMetadataRepository _metadataRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdjunctLabPermissionStrategy> _logger;

    private const string MetadataCacheKey = "Sys_PermissionMetadata_All";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public AdjunctLabPermissionStrategy(
        IPermissionMetadataRepository metadataRepository,
        IMemoryCache cache,
        ILogger<AdjunctLabPermissionStrategy> logger)
    {
        _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool AppliesTo(bool isPrimary) => !isPrimary;

    public async Task<long> ValidateAndFilterMaskAsync(long requestedMask, CancellationToken cancellationToken = default)
    {
        // 快速通關：若未請求任何權限，直接回傳空遮罩
        if (requestedMask == 0)
        {
            return 0L;
        }

        try
        {
            // 優先由記憶體快取讀取全域權限元資料，避免頻繁查詢資料庫
            if (!_cache.TryGetValue(MetadataCacheKey, out IReadOnlyCollection<PermissionMetadata>? metadataList) || metadataList == null)
            {
                metadataList = await _metadataRepository.GetAllMetadataAsync(cancellationToken)
                    .ConfigureAwait(false);

                _cache.Set(MetadataCacheKey, metadataList, CacheDuration);
            }

            if (metadataList.Count == 0)
            {
                _logger.LogWarning("[AdjunctLabPermissionStrategy] 無法取得系統權限中繼資料，為確保安全性，將拒絕所有權限請求。");
                return 0L;
            }

            long finalMask = 0L;

            // 定義兼任實驗室允許的操作敏感度分級
            var allowedCategories = new[] { ActionCategory.Basic, ActionCategory.Operational };

            foreach (var metadata in metadataList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 防禦性檢查：忽略無效的位元位置，避免產生位移異常
                if (metadata.BitPosition is < 0 or > 63)
                {
                    continue;
                }

                long currentBit = 1L << metadata.BitPosition;

                // 檢查使用者申請的遮罩是否包含此功能位元
                if ((requestedMask & currentBit) == currentBit)
                {
                    // 檢查該功能的敏感度是否在兼任實驗室的允許範圍內
                    if (allowedCategories.Contains(metadata.Category))
                    {
                        finalMask |= currentBit;
                    }
                    else
                    {
                        // 記錄被安全策略剔除的權限，便於稽核與除錯
                        _logger.LogDebug("兼任實驗室過濾：已自動剔除位元 {BitPosition} (權限: {PermissionKey})，因其敏感級別為 {Category}，不符合兼任資格。",
                            metadata.BitPosition, metadata.PermissionKey, metadata.Category);
                    }
                }
            }

            return finalMask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[AdjunctLabPermissionStrategy] 權限遮罩驗證與過濾作業已取消。");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AdjunctLabPermissionStrategy] 兼任實驗室權限驗證過程發生未預期的系統異常。");
            throw;
        }
    }
}