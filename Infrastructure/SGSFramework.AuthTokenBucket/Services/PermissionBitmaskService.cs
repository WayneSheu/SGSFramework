#nullable enable

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.Permissions.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 功能級 Bitmask 計算與解碼服務 (已導入快取與記憶體配置最佳化)
/// </summary>
public sealed class PermissionBitmaskService : IPermissionBitmaskService
{
    private readonly IPermissionMetadataRepository _metadataRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionBitmaskService> _logger;

    private const string MetadataCacheKey = "Sys_PermissionMetadata_All";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public PermissionBitmaskService(
        IPermissionMetadataRepository metadataRepository,
        IMemoryCache cache,
        ILogger<PermissionBitmaskService> logger)
    {
        _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, long>> CalculateModuleBitmasksAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var targetPermissions = permissions as HashSet<string>
            ?? new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

        if (targetPermissions.Count == 0)
        {
            return result;
        }

        try
        {
            var metadataList = await _metadataRepository.GetByPermissionKeysAsync(targetPermissions, cancellationToken)
                .ConfigureAwait(false);

            if (metadataList.Count == 0)
            {
                _logger.LogWarning("[PermissionBitmaskService] 未找到與傳入權限相對應的 Metadata 紀錄。");
                return result;
            }

            foreach (var metadata in metadataList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(metadata.PermissionKey))
                {
                    ApplyBitmask(result, metadata.PermissionKey, metadata.BitPosition);
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionBitmaskService] Bitmask 計算作業已取消。");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionBitmaskService] 計算權限 Bitmask 時發生系統異常。");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> DecodeBitmaskToPermissionsAsync(
        Dictionary<string, long> bitmaskMap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmaskMap);

        if (bitmaskMap.Count == 0)
        {
            return new List<string>();
        }

        try
        {
            // 優先由記憶體快取讀取全域權限元資料，避免頻繁查詢資料庫
            if (!_cache.TryGetValue(MetadataCacheKey, out IReadOnlyCollection<PermissionMetadata>? metadataList) || metadataList == null)
            {
                var dbMetadata = await _metadataRepository.GetAllMetadataAsync(cancellationToken)
                    .ConfigureAwait(false);

                metadataList = dbMetadata;
                _cache.Set(MetadataCacheKey, metadataList, CacheDuration);
            }

            if (metadataList.Count == 0)
            {
                _logger.LogWarning("[PermissionBitmaskService] 未能取得系統權限 Metadata，無法解碼位元遮罩。");
                return new List<string>();
            }

            // 直接使用 HashSet 避免後續的 Distinct() 操作產生額外記憶體配置
            var resultSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var metadata in metadataList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(metadata.PermissionKey))
                {
                    CheckAndAddPermission(metadata.PermissionKey, metadata.BitPosition, bitmaskMap, resultSet);
                }
            }

            return new List<string>(resultSet);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionBitmaskService] Bitmask 解碼作業已取消。");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionBitmaskService] 解碼權限 Bitmask 時發生系統異常。");
            throw;
        }
    }

    private void ApplyBitmask(Dictionary<string, long> result, string rawPermissionKey, int bitPosition)
    {
        if (bitPosition is < 0 or > 63)
        {
            _logger.LogError("PermissionKey {Key} 具有無效的位元位置 {Position}。必須介於 0 到 63 之間。", rawPermissionKey, bitPosition);
            return; // 拒絕靜默覆蓋，直接跳過無效權限以確保資安
        }

        string groupKey = ExtractFeaturePrefix(rawPermissionKey);
        long currentMask = 1L << bitPosition;

        if (result.TryGetValue(groupKey, out long existingMask))
        {
            result[groupKey] = existingMask | currentMask;
        }
        else
        {
            result[groupKey] = currentMask;
        }
    }

    private void CheckAndAddPermission(
        string permissionKey,
        int bitPosition,
        Dictionary<string, long> bitmaskMap,
        HashSet<string> resultSet)
    {
        if (bitPosition is < 0 or > 63)
        {
            return;
        }

        string groupKey = ExtractFeaturePrefix(permissionKey);
        if (bitmaskMap.TryGetValue(groupKey, out long storedBitmask))
        {
            long targetMask = 1L << bitPosition;

            if ((storedBitmask & targetMask) == targetMask)
            {
                resultSet.Add(permissionKey);
            }
        }
    }

    /// <summary>
    /// 高效能字串擷取：使用 Span 避免 Split() 產生的記憶體配置
    /// </summary>
    private static string ExtractFeaturePrefix(string permissionKey)
    {
        var span = permissionKey.AsSpan();
        int firstDotIndex = span.IndexOf('.');

        if (firstDotIndex >= 0)
        {
            int nextDotOffset = span.Slice(firstDotIndex + 1).IndexOf('.');
            if (nextDotOffset >= 0)
            {
                // 擷取包含前兩個區段的字串，例如 "Module.Feature"
                return span.Slice(0, firstDotIndex + 1 + nextDotOffset).ToString();
            }
        }

        return permissionKey;
    }
}