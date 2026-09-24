#nullable enable

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 功能級 Bitmask 計算與解碼服務
/// </summary>
public sealed class PermissionBitmaskService : IPermissionBitmaskService
{
    private readonly IPermissionMetadataRepository _metadataRepository;
    private readonly ILogger<PermissionBitmaskService> _logger;

    public PermissionBitmaskService(
        IPermissionMetadataRepository metadataRepository,
        ILogger<PermissionBitmaskService> logger)
    {
        _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, long>> CalculateModuleBitmasksAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var targetPermissions = permissions as HashSet<string> ?? new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

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

        var result = new List<string>();
        if (bitmaskMap.Count == 0)
        {
            return result;
        }

        try
        {
            var metadataList = await _metadataRepository.GetAllMetadataAsync(cancellationToken)
                .ConfigureAwait(false);

            if (metadataList.Count == 0)
            {
                _logger.LogWarning("[PermissionBitmaskService] 未能取得系統權限 Metadata，無法解碼位元遮罩。");
                return result;
            }

            foreach (var metadata in metadataList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(metadata.PermissionKey))
                {
                    CheckAndAddPermission(metadata.PermissionKey, metadata.BitPosition, bitmaskMap, result);
                }
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

    private static void ApplyBitmask(Dictionary<string, long> result, string rawPermissionKey, long bitPosition)
    {
        if (string.IsNullOrWhiteSpace(rawPermissionKey)) return;

        string groupKey = ExtractFeaturePrefix(rawPermissionKey);
        int normalizedBit = (int)(bitPosition % 64);
        long currentMask = (1L << normalizedBit);

        if (result.TryGetValue(groupKey, out long existingMask))
        {
            result[groupKey] = existingMask | currentMask;
        }
        else
        {
            result[groupKey] = currentMask;
        }
    }

    private static void CheckAndAddPermission(
        string permissionKey,
        long bitPosition,
        Dictionary<string, long> bitmaskMap,
        List<string> resultList)
    {
        string groupKey = ExtractFeaturePrefix(permissionKey);
        if (bitmaskMap.TryGetValue(groupKey, out long storedBitmask))
        {
            int normalizedBit = (int)(bitPosition % 64);
            long targetMask = (1L << normalizedBit);

            if ((storedBitmask & targetMask) == targetMask)
            {
                resultList.Add(permissionKey);
            }
        }
    }

    private static string ExtractFeaturePrefix(string permissionKey)
    {
        string[] parts = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0]}.{parts[1]}";
        }
        return permissionKey;
    }
}